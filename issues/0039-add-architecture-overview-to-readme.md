---
id: 39
title: "Add architecture overview and development instructions to README"
status: open
created: 2026-02-28
author: claude
labels: [documentation]
priority: low
---

## Description

The README (446 lines) is thorough for deployment but lacks:

1. **Architecture overview**: No explanation of the 5-project structure (`Core`, `Data`, `Email`, `Worker`, `Admin`), their relationships, or overall data flow (poll -> store -> aggregate -> email).

2. **Background services**: No mention that the worker runs 3 concurrent services (`PollingService`, `TokenRefreshService`, `EmailSchedulerService`) with independent schedules.

3. **Development setup**: No instructions for running tests, no mention of test projects, no local development guidance.

4. **Troubleshooting**: No documentation of common failure modes (expired tokens, SES sandbox, incorrect bridge ID, timezone not found).

5. **Battery reading type**: The `DeviceReading.Value` comment only documents "motion" and "temperature", but code also stores "battery". Data retention cleanup behavior is undocumented.

6. **PUID/PGID naming mismatch**: docker-compose.yml uses `PUID`/`PGID` but README references `UID`/`GID`.

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. CLAUDE.md already contains architecture overview (lines 17-56). README has a Building from source section. The PUID/PGID naming mismatch (docker-compose uses PUID/PGID, README says UID/GID) is a genuine bug that should be its own issue. The issue bundles 4+ separate concerns with different priorities.
