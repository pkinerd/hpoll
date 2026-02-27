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
## Hue API Architecture (verified from API docs & scripts)
### Two-layer architecture
The Hue API has two access layers, both using the same CLIP v2 resource model:
| Layer | Base URL | Auth | Use case |
|---|---|---|---|
| **Local CLIP API** | `https://{bridge_ip}/clip/v2` | `hue-application-key` header (permanent, no expiry) | Direct bridge access on LAN. Supports SSE EventStream for real-time push. Self-signed TLS cert. |
| **Remote Cloud API** | `https://api.meethue.com/route/clip/v2` | **Dual-header:** `Authorization: Bearer <oauth_token>` + `hue-application-key: <app_key>` | Cloud proxy to bridge. OAuth2 access tokens expire every 7 days. Polling-only (no SSE remotely). |
hpoll uses the **Remote Cloud API** exclusively. The cloud proxy relays requests to the customer's bridge — it does **not** cache any state. If the bridge is offline, cloud returns HTTP 503/timeout.
### Remote API authentication flow (verified via shell scripts)
The full onboarding sequence, demonstrated in `docs/scripts/`:
| Step | Script | Endpoint | Method | Auth | Purpose |
|---|---|---|---|---|---|
| 1. Authorize | `1_generate_auth_url.sh` | `GET /v2/oauth2/authorize` | Browser redirect | None | User grants OAuth2 access |
| 2. Token exchange | `2_token_exchange.sh` | `POST /v2/oauth2/token` | POST form | Basic (`client_id:client_secret`) | Exchange auth code for access + refresh tokens |
| 3a. Link button | `3_finalize_auth.sh` | `PUT /route/api/0/config` | PUT JSON | Bearer token | Simulate pressing physical bridge button (cloud equivalent) |
| 3b. Register app | `3_finalize_auth.sh` | `POST /route/api` | POST JSON | Bearer token | Get bridge-specific `username` (= `hue-application-key` / app key). **One-time per bridge.** |
| 4. Query data | `4_test_connection.sh` | `GET /route/clip/v2/resource/{type}` | GET | Bearer + app key | All ongoing data requests use this dual-header pattern |
| 5. Refresh | `6_refresh_token.sh` | `POST /v2/oauth2/token` | POST form | Basic (`client_id:client_secret`) | Refresh expired access token using refresh token |
**Credential model per hub:**
- `client_id` + `client_secret` — shared across all hubs (one app registration)
- `access_token` — per-hub, expires every 7 days, refreshable
- `refresh_token` — per-hub, used to obtain new access tokens
- `hue_application_key` (username) — per-hub, **permanent** (never expires after initial bridge registration)
### Remote API constraints
| Constraint | Detail | Impact |
|---|---|---|
| **Auth** | OAuth2 + permanent app key. Access tokens expire every 7 days. | Must automate refresh ~every 1-2 days for safety margin |
| **Refresh token lifetime** | Not clearly documented in API specs | **Verify during POC API exploration** — the refresh token may also have an expiry |
| **Events** | Remote API is polling-only (no webhooks/SSE remotely) | Polling is the only remote integration pattern |
| **Rate limits** | **50,000 calls/day per app registration (shared across ALL users of the app)**. HTTP 429 when exceeded. Signify can also revoke access. Quota increases rarely granted. **No rate limit headers** (`Retry-After`, `X-RateLimit-*`) are documented. | See rate limit analysis below |
| **Bulk retrieval** | `GET /resource` returns **ALL** resources of **every** type in one call. Per-type endpoints (e.g. `/resource/motion`) return all instances of that type. | **1 call per hub per poll cycle is achievable** — see polling strategy below |
| **Historical data** | None — API returns current state snapshots only | All time series storage must be built in-house |
| **No pagination** | No documented pagination on any endpoint | Single response contains all resources — fine for typical home setups |
| **Hub status** | No explicit bridge online/offline field. Cloud proxy returns HTTP 503 or timeout when bridge unreachable. `zigbee_connectivity` and `wifi_connectivity` resources provide per-device reachability when bridge is online. | Hub offline = HTTP 503/timeout from cloud proxy. Device offline = `zigbee_connectivity.status` values: `connected`, `disconnected`, `connectivity_issue` (5 values). Cloud does NOT cache — no data available while bridge is offline. |
| **Device state** | Full CLIP v2 resource model: 144 operations, 85 paths, 43 resource types. See resource reference below. | Far more data available than needed for initial scope |
### Rate limit analysis
The 50k/day limit is **per app (client_id), shared across all customers**. With the `GET /resource` single-call option, each poll cycle needs only **1 API call per hub**:
| Calls/hub/poll | Polls/day (hourly) | Calls/hub/day | Max hubs at 50k |
|---|---|---|---|
| **1** | 24 | **24** | **~2,083** |
| 2 | 24 | 48 | ~1,041 |
| 3 | 24 | 72 | ~694 |
**Recommended POC polling strategy:** Use **3 calls per hub per poll cycle** — `GET /resource/motion` + `GET /resource/temperature` + `GET /resource/device` — to stay well within limits while fetching only needed data. The device call is needed to resolve sensor IDs to human-readable names (as demonstrated in `docs/scripts/8_combo_jq.sh`). At 3 calls/hub, supports ~694 hubs comfortably.
**Alternative:** Use `GET /resource` (single call, returns everything) for simplicity. Slightly larger responses but only 1 call per hub per cycle, supporting ~2,000 hubs. Recommended when scope expands beyond motion + temperature.
### CLIP v2 resource model — key concepts
**Resource referencing:** Every resource has a UUID `id` and a `type`. Cross-references use `{ "rid": "<uuid>", "rtype": "<type>" }` pairs in fields like `owner`, `children`, `services`.
**Device-service decomposition:** A physical device is one `device` resource with a `services[]` array linking to its capabilities. For example, a Hue motion sensor device has services of type: `motion`, `temperature`, `light_level`, `device_power`, and `zigbee_connectivity`. To resolve a motion sensor to its device name, follow `owner.rid` from the motion service back to the device's `metadata.name`.
**Standard response envelope (all endpoints):**
```json
{
  "errors": [],
  "data": [ ... ]
}
```
On success: `errors` is empty, `data` contains results (always an array). On error: `errors` contains `{ "description": "..." }` objects. HTTP 207 indicates partial success — check both arrays.
**Relevant error codes:** 200 OK, 201 Created, 207 Multi-Status (partial success), 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 429 Too Many Requests (rate limited), 503 Service Unavailable (busy/offline), 507 Insufficient Storage.
**Retry strategy:** Exponential backoff for 429/503: initial 100-500ms, double each retry, cap at 5-10s, give up after 3-5 retries.
### Resource reference — monitoring-relevant types
#### Motion sensor (`GET /resource/motion`) — **POC primary focus**
```
motion.motion_report.motion    : boolean  — true if motion currently detected
motion.motion_report.changed   : datetime — last time value changed
enabled                        : boolean  — true when sensor is activated
sensitivity.sensitivity        : integer  — 0 to sensitivity_max
owner.rid                      : uuid     — links back to parent device
```
Note: `motion.motion` and `motion.motion_valid` are **deprecated** — use `motion_report` sub-object. Motion is valid when `motion_report` property is present, invalid when absent.
#### Device (`GET /resource/device`) — **needed for name resolution**
```
metadata.name                  : string   — human-readable name (1-32 chars)
metadata.archetype             : string   — device type (64 archetypes)
product_data.model_id          : string   — hardware model
product_data.product_name      : string   — product name
product_data.software_version  : string   — firmware version
services[]                     : array    — {rid, rtype} references to all sub-services
```
#### Device power (`GET /resource/device_power`) — **battery monitoring**
```
power_state.battery_level      : integer  — 0-100 percentage
power_state.battery_state      : string   — "normal", "low", "critical"
owner.rid                      : uuid     — links back to parent device
```
Only present for battery-powered devices (sensors, switches). Mains-powered devices (lights) do not have this resource.
#### Zigbee connectivity (`GET /resource/zigbee_connectivity`) — **device reachability**
```
status                         : string   — "connected", "disconnected", "connectivity_issue" (5 values)
mac_address                    : string   — ZigBee MAC address
owner.rid                      : uuid     — links back to parent device
```
#### Temperature sensor (`GET /resource/temperature`) — **future scope**
```
temperature.temperature_report.temperature : number   — degrees Celsius
temperature.temperature_report.changed     : datetime — last change
enabled                                    : boolean
```
#### Light level sensor (`GET /resource/light_level`) — **future scope**
```
light.light_level_report.light_level : integer  — logarithmic: 10000 * log10(lux) + 1
light.light_level_report.changed     : datetime — last change
enabled                              : boolean
```
To convert: `lux = 10^((light_level - 1) / 10000)`.
#### Light (`GET /resource/light`) — **future scope**
```
on.on                          : boolean  — on/off state
dimming.brightness             : number   — 0-100 percentage
color_temperature.mirek        : integer  — 50-1000 mirek (colour temp)
color.xy.x / color.xy.y       : number   — CIE 1931 chromaticity (0-1)
mode                           : string   — "normal" or "streaming"
```
#### Contact sensor (`GET /resource/contact`) — **future scope**
```
contact_report.state           : string   — "contact" or "no_contact"
contact_report.changed         : datetime — last change
enabled                        : boolean
```
#### Room (`GET /resource/room`) — **context for device location**
```
metadata.name                  : string   — room name (1-32 chars)
metadata.archetype             : string   — room type (40 archetypes: living_room, kitchen, etc.)
children[]                     : array    — {rid, rtype} references to devices in the room
services[]                     : array    — aggregated services (grouped_light, grouped_motion, etc.)
```
A device can belong to exactly **one** room. Zones allow overlapping membership.
### Remaining research items (verify during POC)
1. ~~Exact Remote API data coverage~~ → **Resolved.** Full CLIP v2 resource model is available via the cloud proxy. 144 operations, 43 resource types.
2. Refresh token lifetime and failure modes — **still unverified**. The refresh token may have an expiry; test during POC onboarding.
3. ~~Cloud API relay vs cache~~ → **Resolved.** Cloud is a pure relay. No cached state. Bridge offline = no data.
4. ~~Exact API calls needed per hub~~ → **Resolved.** See polling strategy above. 1-2 calls per hub per cycle.
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
| **Container registry** | Docker Hub | Same | CI builds and pushes image on every merge to main/dev. VPS pulls from registry to deploy |
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
**Initial scope:** Motion sensor last-seen date/time and temperature readings only. These are the primary monitoring data points — motion detection (as demonstrated by the exploration scripts, particularly `8_combo_jq.sh`) and ambient temperature from the same multi-sensor devices. This exercises the full data pipeline: OAuth → cloud proxy → device/motion/temperature cross-join → storage → email, without the complexity of lights, scenes, or rules.
**Success criteria:** Data flows from Hue hub → storage → summary email showing last motion timestamps and current temperatures per sensor device.
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
          "HueApplicationKey": "abcdef1234567890abcdef1234567890",
          "AccessToken": "...",
          "RefreshToken": "...",
          "TokenExpiresAt": "2025-03-10T00:00:00Z"
        }
      ]
    }
  ]
}
```
Note: `HueApplicationKey` is the bridge-specific "username" obtained during the one-time bridge registration step (script `3_finalize_auth.sh`). It is **permanent** and never expires. It must be sent alongside the OAuth Bearer token on every CLIP v2 request via the `hue-application-key` header.
Tokens and app key are obtained manually during onboarding using the shell scripts in `docs/scripts/`:
1. Admin runs `1_generate_auth_url.sh` → opens URL in browser → grants access → copies auth code
2. Admin runs `2_token_exchange.sh <code>` → obtains access + refresh tokens
3. Admin runs `3_finalize_auth.sh` → simulates link button + registers app → obtains application key (username)
4. Admin verifies with `4_test_connection.sh` → confirms data flows
5. Copies access token, refresh token, and application key into config
6. Restarts the Docker container (or the service hot-reloads config)
This is sufficient for 5-10 customers and avoids building OAuth flow, CLI tools, or web UI at POC stage.
### Build order (risk-first)
#### 1.1 — Prerequisite Research
- **Hue API deep exploration** (Issue #0007): ~~standalone scripts to complete OAuth manually, hit every relevant endpoint, document what data is actually available via the Remote API~~ → **DONE.** Shell scripts in `docs/scripts/` demonstrate full OAuth flow, bridge registration, and data queries. API documentation extracted to `docs/hue.api/` with OpenAPI specs and endpoint reference.
- ~~Verify exact API calls needed per hub~~ → **Resolved.** POC needs 2 calls per hub: `GET /route/clip/v2/resource/motion` + `GET /route/clip/v2/resource/device`. Device call needed to resolve motion sensor IDs to human-readable names via the `services[].rid` → `owner.rid` join pattern.
- ~~Confirm hub offline detection behaviour~~ → **Resolved.** Cloud proxy returns HTTP 503/timeout when bridge unreachable. Per-device: `zigbee_connectivity.status` = `disconnected`/`connectivity_issue`.
- ~~Confirm whether Cloud API is relay or caches any state~~ → **Resolved.** Pure relay — no cached state, no historical data.
- Verify refresh token lifetime and failure modes — **still open**, must test during POC onboarding
- Legal review of ToS interpretation for commercial monitoring model — **still open**
- **Gate:** ~~Do not proceed to full build until API viability is confirmed~~ → **API viability confirmed.** Remaining gate items: refresh token lifetime verification and ToS legal review.
#### 1.2 — Project Scaffolding
- .NET solution structure with Dockerfile (multi-stage build: SDK image for build/publish, runtime image for final layer)
- Update `.github/workflows/build-and-test.yml` to build and push Docker image to Docker Hub after successful build — triggers on push to main/dev. Image tagged with `latest` and short commit SHA (e.g. `hpoll:abc1234`). Requires `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` repository secrets
- EF Core DbContext with SQLite provider + initial migration
- Config binding: `IOptions<List<CustomerConfig>>` to read customers/hubs from `appsettings.json`
- On startup: seed/sync DB from config (create/update customer and hub records)
- Data model:
  - `Customers` — id, name, email, status, created_at, updated_at
  - `Hubs` — id, customer_id (FK), hue_bridge_id, hue_application_key (encrypted), access_token (encrypted), refresh_token (encrypted), token_expires_at, status, last_polled_at, last_success_at, consecutive_failures
  - `Devices` — id, hub_id (FK), hue_device_id, device_type, name, room_name, last_known_state (JSON). The `services[]` array from the Hue device resource maps sub-resources (motion, temperature, connectivity, power) back to this device via `owner.rid`.
  - `DeviceReadings` — id, device_id (FK), timestamp, reading_type, value (JSON). **POC scope:** reading_type is `motion` (value: `{ "motion": bool, "changed": "datetime" }`) or `temperature` (value: `{ "temperature": 21.5, "changed": "datetime" }`)
  - `PollingLog` — id, hub_id (FK), timestamp, success (bool), error_message, api_calls_made
#### 1.3 — Hue API Client
- API client targeting `https://api.meethue.com/route/clip/v2/resource/` with dual-header auth:
  - `Authorization: Bearer <access_token>`
  - `hue-application-key: <application_key>`
- **POC endpoints (3 calls per hub per poll cycle):**
  1. `GET /resource/motion` — all motion sensors: `motion_report.motion` (bool), `motion_report.changed` (datetime), `owner.rid` (device link)
  2. `GET /resource/temperature` — all temperature sensors: `temperature_report.temperature` (°C), `temperature_report.changed` (datetime), `owner.rid` (device link)
  3. `GET /resource/device` — all devices: `metadata.name`, `services[]`, `product_data`. Needed to resolve motion/temperature sensor IDs to human-readable device names via the `owner.rid` → device join (as demonstrated in `docs/scripts/8_combo_jq.sh`)
- **Cross-resource join pattern:** Motion and temperature sensors link to their parent device via `owner.rid`. To display "Kitchen sensor: last motion 2h ago, 21.5°C", fetch motion + temperature + device, then join on `motion.owner.rid == device.id` and `temperature.owner.rid == device.id`.
- Parse Hue CLIP v2 JSON responses (standard `{ "errors": [], "data": [...] }` envelope) into domain models
- Handle HTTP 503/timeout (bridge offline), 429 (rate limited — no `Retry-After` header, use exponential backoff), 401 (token expired)
- **No OAuth flow in code** — tokens and app key come from config, obtained manually via shell scripts
- **This is the riskiest integration — prove it immediately after scaffolding**
#### 1.4 — Token Refresh Service
- `BackgroundService` running daily (or every 1-2 days)
- For each hub: `POST https://api.meethue.com/v2/oauth2/token` with `grant_type=refresh_token` and Basic auth (`client_id:client_secret` base64-encoded)
- Update access token (and potentially new refresh token) in DB + write back to config (or just DB if config is seed-only)
- Note: the `hue_application_key` (username) never expires and does not need refreshing
- On failure: log error, flag hub, stop polling that hub
- Simple — just one API call per hub per refresh cycle
#### 1.5 — Polling Service
- `BackgroundService` that runs on a timer (hourly)
- For each active hub: 3 API calls per cycle:
  1. `GET /resource/motion` → extract `motion_report.motion`, `motion_report.changed`, `owner.rid` for each sensor
  2. `GET /resource/temperature` → extract `temperature_report.temperature`, `temperature_report.changed`, `owner.rid` for each sensor
  3. `GET /resource/device` → build lookup map of `device.id` → `device.metadata.name` for human-readable names
  4. Join: for each motion/temperature sensor, resolve `owner.rid` against device map to get device name
  5. Store: write `DeviceReadings` rows for each sensor with reading_type `motion` or `temperature`
- Note: `motion_report` may be absent (null) when no motion data is valid — handle gracefully. Same for `temperature_report`.
- Track success/failure per hub in `PollingLog`
- Detect hub offline via HTTP 503/timeout; track consecutive failures
- Handle 401 (token expired) by triggering immediate refresh, then retry
- Handle 429 (rate limited) with exponential backoff (100ms initial, double each retry, cap 10s, max 5 retries)
#### 1.6 — Email Generation & Sending
- 1-2 email templates (MJML compiled to HTML, or Razor-based with inline CSS)
- **POC email content (motion + temperature scope):**
  - Per-sensor summary: device name, room (if available), last motion detected timestamp (with "X hours ago" relative time), current temperature reading
  - Simple table or card layout — one row/card per sensor device
  - Basic alerts: "no motion detected in 24h" (possible sensor issue), temperature outside normal range
- `BackgroundService` that sends emails on a fixed schedule (e.g. 8 AM daily)
- Send via SendGrid free tier or similar SMTP
#### 1.7 — Deploy
- `docker pull` from Docker Hub + `docker run` on a VPS — image is built and pushed by CI (see Phase 1.2), no local build needed on the server
- SQLite database file persisted via Docker volume
- Runtime configuration via environment variables passed to `docker run` (e.g. `-e ConnectionStrings__Default=... -e SendGrid__ApiKey=...`). `appsettings.json` baked into the image contains non-secret defaults; secrets always via env vars
- Volume mounts: `-v /data/hpoll:/app/data` for SQLite DB persistence
- Example deployment: `docker pull <dockerhub-user>/hpoll:latest && docker run -d --restart unless-stopped -v /data/hpoll:/app/data -e "Customers__0__Name=..." <dockerhub-user>/hpoll:latest`
- **DB backup/recovery:** admin can download the SQLite `.db` file from the host at any time. On redeployment, mount the existing DB file and the service resumes with all accumulated readings and refreshed tokens — no need to re-register hubs. Config tokens are only used for initial seeding; after that, the DB holds the latest refreshed tokens.
### What to skip at POC
- OAuth flow in code (manual token setup via shell scripts in `docs/scripts/`)
- Admin UI or CLI tool (config-driven)
- PostgreSQL (use SQLite — single process, no concurrency)
- User-preferred send times (fixed schedule)
- Sophisticated rules engine (just basic "no motion in 24h" / hub offline detection)
- Lights, scenes, contact sensors, buttons, light level sensors (future scope — only motion + temperature initially)
- Battery level monitoring via `device_power` (add in next iteration)
- Zigbee connectivity monitoring (add in next iteration)
- Room-based grouping and context (add when expanding beyond motion/temperature)
- Rate limit budgeting (irrelevant at 5-10 customers, and 3 calls/hub is well within limits)
- Automated tests (manual verification)
- Full CD automation (deploy manually via `docker pull` + `docker run` — but image build+push is automated in CI)
- Disconnection recovery / backfill logic
### POC deliverables
1. Docker container running three background services (poll, refresh, email)
2. Config-driven customer/hub setup with manually-obtained tokens and application key (via `docs/scripts/`)
3. Hourly polling of motion sensor status and temperature readings, stored in SQLite
4. Daily token refresh keeping OAuth access tokens valid (application key is permanent)
5. Daily summary email showing per-sensor: device name, last motion timestamp, current temperature
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
- Fill in `.github/workflows/build-and-test.yml` TODOs: `dotnet build`, `dotnet test` (Docker build+push already wired up from POC Phase 1.2)
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
  ├── [DONE] API research spike (Issue #0007): shell scripts, API docs, OpenAPI specs extracted
  ├── [DONE] Verify available endpoints and data coverage: 144 ops, 43 resource types via cloud proxy
  ├── [DONE] Verify cloud API behaviour: pure relay, no cache, 503 when bridge offline
  ├── [OPEN] Verify refresh token lifetime and failure modes (test during POC onboarding)
  └── [OPEN] Legal review of ToS interpretation for commercial monitoring model

POC (build order — config-driven, no UI — motion + temperature scope):
  1. .NET solution scaffolding + Dockerfile + SQLite
  2. Data model + EF Core migrations + config binding (incl. HueApplicationKey)
  3. Hue API client: GET /resource/motion + /resource/temperature + /resource/device
     with dual-header auth (Bearer + hue-application-key). Cross-resource join for names.
  4. Token refresh BackgroundService (daily) — POST /v2/oauth2/token with Basic auth
  5. Polling BackgroundService (hourly) — 3 calls/hub: motion, temperature, device
  6. Email templates: per-sensor last motion timestamp + temperature reading
  7. CI builds + pushes Docker image to Docker Hub; deploy to VPS via docker pull + docker run
MVP (build after POC validated):
  1. Migrate to PostgreSQL + Docker Compose
  2. OAuth flow in code + admin web portal (Razor Pages)
  3. Token lifecycle robustness (retry + backoff + admin flags)
  4. Job scheduling upgrade (Hangfire/Quartz.NET)
  5. Rules & alerting engine
  6. Per-customer email scheduling
  7. Security hardening
  8. CI/CD pipeline + automated tests (Docker build+push already from POC)
  9. Operational tooling (logging, metrics, rate tracking)
```
---
## Key Technical Risks
| Risk | Severity | Mitigation | Status |
|---|---|---|---|
| **Hue OAuth flow complexity** | High | Shell scripts in `docs/scripts/` demonstrate full flow end-to-end. Dual-header auth pattern (Bearer + app key) now documented. | **Mitigated** — scripts proven |
| **Cloud API is a relay (no cached data)** | Medium | **Confirmed:** pure relay, no cache. Polling misses during bridge downtime are permanent gaps. Design for graceful gap handling. | **Verified** — design accordingly |
| **Rate limit (50k/day per-app, shared)** | Medium | With 3 calls/hub (motion + temperature + device), supports ~694 hubs. With `GET /resource` (1 call), supports ~2,083. Track daily usage at MVP. Gateway option as escape hatch. | **Quantified** |
| **Refresh token lifetime unknown** | Medium | Not documented in API specs. Test during POC onboarding. If limited, may need more frequent refresh or re-auth flow. | **Open — verify during POC** |
| **Token refresh reliability** | Medium | Refresh every 1-2 days (not at expiry). Monitor refresh failures as key health metric. `POST /v2/oauth2/token` with Basic auth. | Known pattern |
| **Cross-resource join complexity** | Low | Motion/temperature → device name resolution via `owner.rid` join. Pattern demonstrated in `8_combo_jq.sh`. Straightforward in C#. | **Mitigated** — pattern proven |
| **Email client compatibility** | Low | MJML is purpose-built for this. Test with real clients during POC | Known approach |
---
## Existing Files to Modify
| File | When | Change |
|---|---|---|
| `.github/workflows/build-and-test.yml` | POC (Phase 1.2) | Fill in TODO build steps with `dotnet build`. Add Docker build+push to Docker Hub job (login, build, tag with `latest` + commit SHA, push). Requires `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` secrets. Test steps filled in at MVP (Phase 2.9) with `dotnet test` |
| `README.md` | POC (Phase 1.2) | Update with project description, setup instructions |
## Verification
### POC verification
1. Push to main/dev triggers CI workflow: `dotnet build` succeeds, Docker image built and pushed to Docker Hub
2. `docker pull` + `docker run` on VPS starts container: initialises SQLite DB if missing, seeds from config (incl. HueApplicationKey), starts all three services
3. Token refresh keeps OAuth access tokens valid (verify by checking DB/logs after a refresh cycle). Application key remains unchanged (permanent).
4. Hourly polling stores motion sensor timestamps and temperature readings in SQLite (3 API calls per hub: motion, temperature, device)
5. Daily summary email generated showing per-sensor: device name, last motion time, current temperature
6. Email renders correctly on phone and in Gmail/Outlook
7. Simulate bridge offline (unplug hub) → verify 503 detection and consecutive failure tracking
8. Verify cross-resource join: motion/temperature sensor readings are correctly associated with device names
### MVP verification
1. `docker compose up` runs app + PostgreSQL containers
2. `dotnet test` passes (unit + integration)
3. Admin web portal functional: customer CRUD, OAuth hub linking, attention queue
4. Simulate token expiry → verify auto-refresh → simulate failure → verify admin flag
5. Emails send at individual customer-preferred times across timezones
6. Rate limit tracking: verify daily API call counter is accurate
7. Load test: simulate 500+ customers, verify polling completes within hourly window
8. CI pipeline runs end-to-end on push (build, test, Docker image push — image push already working from POC)
