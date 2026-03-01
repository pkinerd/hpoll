---
id: 8
title: "Implementation plan"
status: closed
closed: 2026-03-01
created: 2026-02-27
author: claude
labels: [planning, documentation]
priority: medium
---

## Description

High-level implementation plan for hpoll, a Philips Hue monitoring service. The full plan document is stored at `docs/implementation-plan.md` on this branch.

### Summary

The plan defines a three-phase progressive implementation strategy: POC → MVP → Scale.

**POC (Phase 1)** — Prove the core flow end-to-end for 5–10 customers:
- Config-driven customer/hub setup (tokens obtained manually via Postman, stored in `appsettings.json`)
- Three `BackgroundService` workers: hourly device polling, daily token refresh, daily summary email
- .NET 8+ / C# with EF Core + SQLite, single Docker container on a VPS
- No OAuth flow in code, no admin UI, no CI/CD — manual deployment and verification
- Build order is risk-first: API research spike → scaffolding → Hue API client (highest risk) → token refresh → polling → email → deploy

**MVP (Phase 2)** — Serve tens to hundreds of customers reliably:
- Migrate SQLite → PostgreSQL, Docker Compose (app + DB containers)
- ASP.NET Core Razor Pages admin portal with OAuth flow for hub registration
- Robust token lifecycle with retry, backoff, and admin flagging
- Rules & alerting engine (battery low, device offline, activity anomalies)
- Per-customer timezone-aware email scheduling with refined templates
- Job scheduling upgrade (Hangfire/Quartz.NET), structured logging, health checks
- CI/CD pipeline + automated tests, security hardening

**Phase 3 (Scale)** — Not built now, but informs early decisions:
- Horizontal polling via distributed Hangfire workers
- TimescaleDB extension for time series at scale
- Rate budgeting or local gateway devices if >2,000 hubs
- Customer-facing portal possible without backend rewrite

### Key constraints
- Hue Remote API: OAuth2 with 7-day token expiry, polling-only (no webhooks), 50k calls/day shared across all users
- Bulk retrieval (1–3 calls/hub) supports 700–2,000 customers within rate limits
- Cloud API is a relay — no cached data when bridge is offline; gaps are permanent

### Key risks
- Hue OAuth flow complexity (mitigated by POC shell scripts)
- Cloud API relay behaviour (verify during API spike)
- Rate limit at scale (bulk retrieval keeps it manageable; gateway as escape hatch)
- Token refresh reliability (refresh well before expiry, monitor failures)

### Related issues
- #0001 — Define objectives and document plan
- #0003 — Setup build pipeline
- #0004 — Setup hosting
- #0005 — Setup CI pipeline for dev/ci
- #0006 — Setup CD pipeline to test and prod
- #0007 — Research API docs for Hue APIs

## Comments
