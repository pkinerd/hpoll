---
id: 64
title: "Sanitize exception messages before persisting in PollingLog"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: medium
---

## Description

When polling fails, the raw exception message (truncated to 500 characters) is stored in `PollingLog.ErrorMessage` and later displayed to admin users on the Hub Detail page.

**File:** `src/Hpoll.Worker/Services/PollingService.cs:250`
**Display:** `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml:122`

Exception messages from `HttpRequestException`, EF Core, and other providers can contain internal details such as connection strings, file paths, IP addresses, or partial credential information. Storing these verbatim in the database and displaying them in the admin UI creates an information disclosure risk.

**Recommended fix:**
- Use curated error messages for known exception types (e.g., "HTTP 401 Unauthorized", "Connection timeout")
- Store a generic message for unknown exception types (e.g., "Internal error during polling")
- Log the full exception details only to application logs (not the database)
- If the full message must be stored, ensure the admin UI does not display it verbatim

**OWASP reference:** A04:2021-Insecure Design

**Source:** Comprehensive review -- security review finding

## Comments

### claude — 2026-03-01

**Comprehensive review update:** In addition to the `PollingLog.ErrorMessage` issue, `HueApiClient` also logs error response bodies at `Warning` level in multiple locations:

- Lines 96-97: `GetMotionSensorsAsync` / `GetTemperatureSensorsAsync` (via `GetResourceAsync`)
- Lines 120-121: `GetDevicesAsync` / `GetDevicePowerAsync` (via `GetResourceAsync`)
- Lines 169-170: `GetResourceAsync` error path
- Lines 200-201: `PostTokenRequestAsync` error path

These responses could contain sensitive bridge configuration details, token fragments, or internal API error information. The error body truncation (`errorBody[..500]`) is applied, but the content itself may still be sensitive.

**Recommendation:** Log only HTTP status code and a generic error category at Warning level. Log the full response body at Debug level only, and ensure Debug logging is disabled in production.
