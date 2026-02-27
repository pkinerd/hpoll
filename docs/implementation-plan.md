# hpoll — Product Feature Plan: POC → MVP → Scale
## Context
hpoll is a Philips Hue monitoring service.
This plan defines a progressive implementation strategy that:
- Proves the core approach with a POC for a handful of customers
- Expands to an MVP capable of serving hundreds to ~2,000 for testing
- Minimises regrettable early decisions that are costly to reverse at scale
- Avoids over-architecting at each stage
---
## Product Requirements Summary
### Admin Management
- Customer CRUD (create, read, update, deactivate — not delete)
- Hub registration via Hue Remote API OAuth2 flow
- Hub ↔ Customer linking: multiple hubs per customer, each hub strictly one customer
- Admin-owned Hue accounts (customers never interact with Hue directly)
- Secure OAuth credential storage (access tokens, refresh tokens, encrypted at rest)
- Health monitoring: customer list, hub status, attention flags
- Attention triggers: hub offline (via Hue Cloud status), OAuth expired/refresh failed, device issues (battery, unreachable)
### Background Service
- Poll all customer hubs via Hue Remote API (hourly frequency is acceptable)
- API supports bulk retrieval — a single call returns all devices on a hub (no per-device calls needed)
- Monitor: hub connectivity (via Hue Cloud API status data), device state (on/off, sensors), battery levels
- Store time series data per customer — **strictly segregated per user, no cross-user aggregation**
- Rules/decision engine: evaluate current + historical data (hours/days rolling windows)
- Trigger notifications and admin alerts — **read-only toward Hue devices (no commands)**
- Handle disconnection recovery and data gap awareness
- Token refresh automation — refresh well before 7-day access token expiry (e.g. every 1-2 days)
- Token failure: auto-retry with exponential backoff, then flag for admin attention and stop polling that hub
- Data retention management (rolling ~7 day window only)
### End-User Output
- Scheduled summary emails at user-preferred time
- Specialised, curated information (not a raw data dump)
- Broad email client compatibility (mobile, desktop, webmail, older clients)
- Simple, aesthetically pleasing, minimal design with basic branding
---
## Hue Remote API Constraints (from research)
| Constraint | Detail | Impact |
|---|---|---|
| **Auth** | OAuth2, access tokens expire every 7 days | Must automate refresh ~every 1-2 days for safety margin |
| **Refresh token lifetime** | Not clearly documented | **Verify during POC API exploration** |
| **Events** | Remote API is polling-only (no webhooks/SSE remotely) | Polling is the only remote integration pattern |
| **Rate limits** | **50,000 calls/day per app registration (shared across ALL users of the app)**. HTTP 429 when exceeded. Signify can also revoke access. Quota increases rarely granted. | See rate limit analysis below |
| **Bulk retrieval** | API endpoints return all devices of a type in one call (e.g. all lights, all sensors) | 1-3 calls per hub per poll cycle, not per device |
| **Historical data** | None — API returns current state snapshots only | All time series storage must be built in-house |
| **Hub status** | No explicit bridge online/offline field. Cloud proxy returns HTTP 503 or timeout when bridge unreachable. `zigbee_connectivity` and `wifi_connectivity` resources provide per-device reachability when bridge is online. | Hub offline = HTTP 503/timeout from cloud proxy. Device offline = `zigbee_connectivity.status: "disconnected"`. Cloud does NOT cache — no data available while bridge is offline. |
| **Device state** | Lights (on/off, brightness, colour), sensors (motion, temp, light level), battery-low flag | Sufficient for monitoring use case |
### Rate limit analysis
The 50k/day limit is **per app (client_id), shared across all customers**. However, since the API supports bulk device retrieval, the per-hub call count is low:
| Calls/hub/poll | Polls/day (hourly) | Calls/hub/day | Max hubs at 50k |
|---|---|---|---|
| 1 | 24 | 24 | ~2,083 |
| 3 | 24 | 72 | ~694 |
| 5 | 24 | 120 | ~416 |

Most customers have 1 hub and a handful of devices. At 1-3 calls per hub, the rate limit supports **700-2,000 customers** — well within MVP target. Rate budgeting becomes relevant only beyond this scale.

**Remaining research items (verify during POC):**
1. Refresh token lifetime and failure modes
2. Exact Remote API data coverage (which device types/properties are available remotely)

### Future option: local gateway
If rate limits or ToS become constraints at scale, a **local gateway device** (e.g. Raspberry Pi shipped with each hub) could:
- Poll the bridge locally via SSE (real-time events, no rate limits, no Remote API dependency)
- Push data to the hpoll cloud
- Eliminate OAuth token management entirely (local API uses different auth)
This is noted as a scaling escape hatch, not a POC/MVP requirement.
---
## Technology Stack
.NET/C# — chosen for strong typing, first-class background service support, enterprise-grade scalability, and familiarity.
| Layer | POC | MVP | Rationale |
|---|---|---|---|
| **Language** | C# / .NET 8+ | Same | Strong typing, excellent async/background worker patterns |
| **Admin interface** | Config-driven (appsettings.json) | ASP.NET Core Razor Pages | POC: customers & tokens in config, manually obtained. MVP: proper admin portal with OAuth flow |
| **Background workers** | `BackgroundService` / `IHostedService` | Same | First-class .NET pattern for long-running background tasks |
| **ORM** | EF Core with SQLite provider | EF Core with Npgsql (PostgreSQL) | EF Core abstracts the provider — swap by changing connection string + NuGet package |
| **Database** | **SQLite** | **PostgreSQL** | SQLite: zero-ops for POC (single file, no container). PostgreSQL: production-grade for MVP with concurrent access, JSON, managed backups |
| **Email templating** | MJML or Razor-based with inline CSS | Same, refined | MJML for max client compatibility; Razor as alternative |
| **Email sending** | SendGrid free tier or SMTP | AWS SES or SendGrid | Cost-effective, reliable |
| **Containerisation** | Docker + SQLite (single container) | Docker Compose (app + PostgreSQL) | POC: single container, SQLite file inside. MVP: separate app + DB containers |
| **Hosting** | Docker on VPS | VPS with managed PostgreSQL | Single container for POC; managed DB for MVP backups |
| **Job scheduling** | Timer-based in `BackgroundService` | Hangfire or Quartz.NET (PostgreSQL persistence) | Simple timers for POC; reliable scheduling with retry for MVP |
---
## Project Structure
### POC structure (minimal)
```
hpoll/
├── src/
│   ├── Hpoll.Worker/                 # Single entry point: all three background services
│   │   ├── Services/
│   │   │   ├── PollingService.cs     # Hourly polling orchestrator
│   │   │   ├── TokenRefreshService.cs # Daily token refresh
│   │   │   └── EmailSchedulerService.cs # Daily email sending
│   │   ├── Program.cs               # Hosts BackgroundServices, reads config
│   │   └── appsettings.json         # Customer + hub config with tokens
│   │
│   ├── Hpoll.Core/                   # Shared domain logic
│   │   ├── Models/                   # Domain entities
│   │   ├── Services/
│   │   │   ├── HueApiClient.cs      # Hue Remote API client
│   │   │   └── HealthEvaluator.cs   # Basic hub/device health
│   │   └── Interfaces/
│   │
│   ├── Hpoll.Data/                   # Data access layer
│   │   ├── HpollDbContext.cs         # EF Core DbContext (SQLite)
│   │   ├── Migrations/
│   │   └── Entities/
│   │
│   └── Hpoll.Email/                  # Email generation
│       ├── Templates/
│       ├── EmailRenderer.cs
│       └── EmailSender.cs
│
├── Dockerfile
├── Hpoll.sln
└── README.md
```
### MVP additions
```
├── src/
│   ├── Hpoll.Web/                    # Admin portal (added at MVP)
│   │   ├── Pages/                    # Razor Pages (dashboard, customers, hubs, OAuth flow)
│   │   ├── wwwroot/
│   │   └── Program.cs
│   ...
├── tests/
│   ├── Hpoll.Core.Tests/
│   └── Hpoll.Worker.Tests/
├── docker-compose.yml                # App + PostgreSQL
└── Dockerfile
```
**Key structural decisions:**
- **POC: single Worker process, config-driven** — customers and hub tokens defined in `appsettings.json`. Tokens obtained manually via Postman/browser, pasted into config. No OAuth flow, no CLI tool, no web UI. Just three background services (poll, refresh, email).
- **Docker + SQLite** — single container, SQLite file inside. Only one process accesses the DB, so no concurrency concerns.
- **MVP: add Hpoll.Web + PostgreSQL** — Razor Pages admin portal with OAuth flow. Migrate to PostgreSQL for concurrent access (web + worker). Docker Compose for multi-container.
- **Hpoll.Core shared library** — Hue API client, domain models, and business logic shared across Worker and Web.
- **Database swap** — EF Core abstracts SQLite (POC) → PostgreSQL (MVP). Same code, different provider.
---
## Phase 1: POC
**Goal:** Prove the core flow end-to-end for 5-10 customers.
**Success criteria:** Data flows from Hue hub → storage → summary email for real devices.
### Customer & hub setup at POC stage
Customers and hubs are configured directly in `appsettings.json`:
```json
{
  "Customers": [
    {
      "Name": "Alice Smith",
      "Email": "alice@example.com",
      "Hubs": [
        {
          "BridgeId": "001788FFFE123456",
          "AccessToken": "...",
          "RefreshToken": "...",
          "TokenExpiresAt": "2025-03-10T00:00:00Z"
        }
      ]
    }
  ]
}
```
Tokens are obtained manually during onboarding:
1. Admin completes OAuth flow via Postman/browser against the Hue Remote API
2. Copies access + refresh tokens into config
3. Restarts the Docker container (or the service hot-reloads config)
This is sufficient for 5-10 customers and avoids building OAuth flow, CLI tools, or web UI at POC stage.
### Build order (risk-first)
#### 1.1 — Prerequisite Research
- **Hue API deep exploration** (Issue #0007): standalone scripts to complete OAuth manually, hit every relevant endpoint, document what data is actually available via the Remote API
- Verify refresh token lifetime and failure modes
- Verify exact API calls needed per hub (e.g. `GET /clip/v2/resource/light`, `/resource/motion`, etc.) and what data each returns
- Confirm hub offline detection behaviour (HTTP 503/timeout)
- Confirm whether Cloud API is relay or caches any state
- Legal review of ToS interpretation for commercial monitoring model
- **Gate:** Do not proceed to full build until API viability is confirmed
#### 1.2 — Project Scaffolding
- .NET solution structure with Dockerfile
- EF Core DbContext with SQLite provider + initial migration
- Config binding: `IOptions<List<CustomerConfig>>` to read customers/hubs from `appsettings.json`
- On startup: seed/sync DB from config (create/update customer and hub records)
- Data model:
  - `Customers` — id, name, email, status, created_at, updated_at
  - `Hubs` — id, customer_id (FK), hue_bridge_id, access_token (encrypted), refresh_token (encrypted), token_expires_at, status, last_polled_at, last_success_at, consecutive_failures
  - `Devices` — id, hub_id (FK), hue_device_id, device_type, name, last_known_state (JSON)
  - `DeviceReadings` — id, device_id (FK), timestamp, reading_type, value (JSON)
  - `PollingLog` — id, hub_id (FK), timestamp, success (bool), error_message, api_calls_made
#### 1.3 — Hue API Client
- API client: bulk retrieval of devices/sensors per hub (e.g. `GET /clip/v2/resource/light`)
- Handle HTTP 503/timeout (bridge offline), 429 (rate limited), 401 (token expired)
- Parse Hue API v2 JSON responses into domain models
- **No OAuth flow in code** — tokens come from config, obtained manually via Postman
- **This is the riskiest integration — prove it immediately after scaffolding**
#### 1.4 — Token Refresh Service
- `BackgroundService` running daily (or every 1-2 days)
- For each hub: call Hue token refresh endpoint with stored refresh token
- Update token in DB + write back to config (or just DB if config is seed-only)
- On failure: log error, flag hub, stop polling that hub
- Simple — just one API call per hub per refresh cycle
#### 1.5 — Polling Service
- `BackgroundService` that runs on a timer (hourly)
- For each active hub: call Hue API (bulk retrieval) → parse response → store device readings
- Track success/failure per hub in `PollingLog`
- Detect hub offline via HTTP 503/timeout; track consecutive failures
- Handle 401 (token expired) by triggering immediate refresh, then retry
- Handle 429 (rate limited) with backoff
#### 1.6 — Email Generation & Sending
- 1-2 email templates (MJML compiled to HTML, or Razor-based with inline CSS)
- Content: device summary, any alerts (battery low, device offline), activity highlights
- `BackgroundService` that sends emails on a fixed schedule (e.g. 8 AM daily)
- Send via SendGrid free tier or similar SMTP
#### 1.7 — Deploy
- `docker build` + `docker run` on a VPS
- SQLite database file persisted via Docker volume
- `appsettings.json` or environment variables for secrets and customer config
- **DB backup/recovery:** admin can download the SQLite `.db` file from the host at any time. On redeployment, mount the existing DB file and the service resumes with all accumulated readings and refreshed tokens — no need to re-register hubs. Config tokens are only used for initial seeding; after that, the DB holds the latest refreshed tokens.
### What to skip at POC
- OAuth flow in code (manual token setup via Postman)
- Admin UI or CLI tool (config-driven)
- PostgreSQL (use SQLite — single process, no concurrency)
- User-preferred send times (fixed schedule)
- Sophisticated rules engine (just basic "hub offline" / "battery low" detection)
- Rate limit budgeting (irrelevant at 5-10 customers)
- Automated tests (manual verification)
- CI/CD automation (deploy manually)
- Disconnection recovery / backfill logic
### POC deliverables
1. Docker container running three background services (poll, refresh, email)
2. Config-driven customer/hub setup with manually-obtained tokens
3. Hourly device state polling and storage in SQLite
4. Daily token refresh keeping access tokens valid
5. Daily summary email generated and sent to a customer
6. Email renders correctly on a phone and in Gmail
---
## Phase 2: MVP
**Goal:** Serve tens to hundreds of customers reliably.
**Success criteria:** System runs unattended, admin can manage all customers via web UI, emails deliver on customer-preferred schedule.
### Infrastructure upgrades (POC → MVP)
- **Database:** Migrate from SQLite to PostgreSQL (swap EF Core provider + connection string)
- **Containerisation:** Docker Compose (app + PostgreSQL containers)
- **Hosting:** managed PostgreSQL for backups and reliability
- **Customer management:** Move from config-driven to database-driven (admin portal creates/manages customers)
### What to add
#### 2.1 — OAuth Flow + Admin Web Portal (new at MVP — replaces config-driven setup)
- **OAuth flow in code:** authorization URL → callback → token exchange, integrated into admin portal
- ASP.NET Core Razor Pages, server-rendered
- Customer list with search, filtering, pagination
- Customer status indicators: healthy / warning / critical
- Attention queue: prioritised list of customers/hubs needing admin action
- Customer detail page: linked hubs, recent polling history, device inventory
- Customer CRUD: add, edit, deactivate/reactivate
- Hub linking/unlinking via OAuth flow
- Token health display: last refreshed, next scheduled refresh, status
- Admin auth: ASP.NET Core Identity with proper password policies
#### 2.2 — Robust Token Lifecycle
- Proactive refresh every 1-2 days with retry + exponential backoff
- On refresh failure: retry 3x with backoff → flag hub as "needs re-auth" → stop polling → alert admin
- Graceful handling of Hue API outages (back off, don't flood)
#### 2.3 — Rules & Alerting Engine
- Simple configurable rules evaluated against device readings:
  - "Alert if battery below X%"
  - "Flag if no motion for Z hours" (may indicate sensor issue)
  - "Highlight if lights on continuously for N hours"
- Rules stored in database, evaluated by the Worker on each poll cycle
- Lookback window support: evaluate against last N hours/days of data
- Outputs: admin dashboard flags + content included in customer emails
#### 2.4 — Email Improvements
- Per-customer timezone-aware scheduling at preferred time
- Refined MJML templates with branding, clean typography, mobile-first
- Template sections that adapt to customer's device types
- Curated narrative content (not just data — "here's what we noticed")
- Email delivery tracking (bounces, failures flagged to admin)
#### 2.5 — Job Scheduling Upgrade
- Replace simple `BackgroundService` timers with Hangfire or Quartz.NET
- PostgreSQL-backed job persistence (no Redis dependency)
- Retry policies, dead letter handling
- Per-customer scheduling for emails
#### 2.6 — Disconnection Handling
- Track polling gaps when a hub is unreachable (HTTP 503/timeout)
- Record the gap and note it in the next summary email
- Rules engine accounts for gaps (don't alert on "no motion" during known downtime)
- (Cloud API is a relay — no historical data or cached state. Gaps in data are permanent, but handled gracefully)
#### 2.7 — Operational Maturity
- Structured logging (Serilog to console/file)
- Health check endpoints (`/health`)
- Rate limit tracking: count daily API calls, warn/throttle if approaching 50k
- Database backups (via managed PostgreSQL)
- Basic metrics: active hubs, polling success rate, email delivery rate
#### 2.8 — Security Hardening
- Hue OAuth tokens encrypted at rest (Data Protection API)
- HTTPS everywhere
- Input validation on all admin forms
- Audit log for admin actions
- SPF, DKIM, DMARC for email sending domain
#### 2.9 — CI/CD & Testing
- Fill in `.github/workflows/build-and-test.yml` TODOs: `dotnet build`, `dotnet test`
- Automated tests: unit tests for rules engine and health evaluation, integration tests for Hue client
- Deploy pipeline aligned with existing Issues #0003, #0005, #0006
### What to skip at MVP
- Customer self-service portal (customers never log in)
- Real-time dashboards or live data views
- Multi-tenancy or team-based admin access
- Horizontal scaling of workers (single instance handles ~700-2,000 customers at hourly polling)
- Local gateway devices (future scale option)
---
## Phase 3: Scale Considerations (light touch)
Not built now — these inform early decisions to minimise regret.
| Concern | Why it won't require a rewrite |
|---|---|
| **Horizontal polling** | Hangfire/Quartz.NET at MVP already supports distributed workers — add instances when needed |
| **Database scale** | PostgreSQL handles millions of rows. Add TimescaleDB extension for time series if needed (no migration — it's a PG extension). Add table partitioning on `DeviceReadings` |
| **Rate limits (>2,000 hubs)** | Implement rate budgeting across customers. Or deploy local gateway devices at customer sites to bypass Remote API entirely (see Future Option above) |
| **Customer portal** | Clean domain layer in `Hpoll.Core` means a customer-facing frontend can be added without rewriting the backend |
| **Advanced analytics** | Time series in PostgreSQL can be queried with SQL or exported to dedicated analytics tools |
---
## Implementation Sequence Summary
```
PREREQUISITE (before writing product code):
  ├── API research spike (Issue #0007): manual OAuth, available endpoints, data coverage
  ├── Verify refresh token lifetime and failure modes
  ├── Verify cloud API behaviour (relay vs cache) for offline bridge scenarios
  └── Legal review of ToS interpretation for commercial monitoring model
POC (build order — config-driven, no UI):
  1. .NET solution scaffolding + Dockerfile + SQLite
  2. Data model + EF Core migrations + config binding
  3. Hue API client (bulk retrieval — highest risk, prove early)
  4. Token refresh BackgroundService (daily)
  5. Polling BackgroundService (hourly)
  6. Email templates + scheduled sending (daily)
  7. Docker build, deploy to VPS, test with real hub + manually-obtained tokens
MVP (build after POC validated):
  1. Migrate to PostgreSQL + Docker Compose
  2. OAuth flow in code + admin web portal (Razor Pages)
  3. Token lifecycle robustness (retry + backoff + admin flags)
  4. Job scheduling upgrade (Hangfire/Quartz.NET)
  5. Rules & alerting engine
  6. Per-customer email scheduling
  7. Security hardening
  8. CI/CD pipeline + automated tests
  9. Operational tooling (logging, metrics, rate tracking)
```
---
## Key Technical Risks
| Risk | Severity | Mitigation |
|---|---|---|
| **Hue OAuth flow complexity** | High | Have POC shell scripts |
| **Cloud API is a relay (no cached data)** | Medium | Verify during API spike. If relay: polling misses during bridge downtime are permanent gaps. Design for graceful gap handling |
| **Rate limit (50k/day per-app, shared)** | Medium | With bulk retrieval (1-3 calls/hub), supports 700-2,000+ hubs. Track daily usage at MVP. Gateway option as escape hatch |
| **Token refresh reliability** | Medium | Refresh every 1-2 days (not at expiry). Monitor refresh failures as key health metric |
| **Email client compatibility** | Low | MJML is purpose-built for this. Test with real clients during POC |
---
## Existing Files to Modify
| File | When | Change |
|---|---|---|
| `.github/workflows/build-and-test.yml` | MVP (Phase 2.9) | Fill in TODO build/test steps with `dotnet build`, `dotnet test` |
| `README.md` | POC (Phase 1.2) | Update with project description, setup instructions |
## Verification
### POC verification
1. `docker run` starts container: initialises SQLite DB if missing, seeds from config, starts all three services
2. Token refresh keeps access tokens valid (verify by checking DB/logs after a refresh cycle)
3. Hourly polling stores device state snapshots in SQLite
4. Daily summary email generated and sent to a test customer
5. Email renders correctly on phone and in Gmail/Outlook
6. Simulate bridge offline (unplug hub) → verify 503 detection and consecutive failure tracking
### MVP verification
1. `docker compose up` runs app + PostgreSQL containers
2. `dotnet test` passes (unit + integration)
3. Admin web portal functional: customer CRUD, OAuth hub linking, attention queue
4. Simulate token expiry → verify auto-refresh → simulate failure → verify admin flag
5. Emails send at individual customer-preferred times across timezones
6. Rate limit tracking: verify daily API call counter is accurate
7. Load test: simulate 500+ customers, verify polling completes within hourly window
8. CI pipeline runs end-to-end on push
