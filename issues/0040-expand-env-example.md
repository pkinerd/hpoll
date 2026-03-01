---
id: 40
title: "Expand .env.example to cover all configuration options"
status: open
created: 2026-02-28
author: claude
labels: [documentation]
priority: low
---

## Description

The `.env.example` covers only ~5 of ~25+ available configuration options, omitting:

- All `Polling__*` settings (BatteryPollIntervalHours, DataRetentionHours, HttpTimeoutSeconds, TokenRefresh*, Health*)
- All `Email__*` advanced settings (BatteryAlertThreshold, BatteryLevelCritical/Warning, SummaryWindowHours/Count, ErrorRetryDelayMinutes)
- PUID/PGID for docker-compose
- AWS credential configuration guidance (env vars vs IAM roles)
- Logging configuration
- ASPNETCORE_ENVIRONMENT

Also, no validation ranges are documented anywhere. For example: what happens if `IntervalMinutes` is 0? If `DataRetentionHours` < `SummaryWindowHours * SummaryWindowCount`? If `BatteryLevelCritical` > `BatteryLevelWarning`?

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. **FABRICATED**: Health* settings do not exist anywhere in the codebase. The .env.example has **10 entries** (not ~5) covering all required credentials. AWS credential guidance claim is false (already present). All missing settings have sensible defaults. Validation ranges belong in a separate code issue, not .env.example.

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY VALID — the core ask has merit, but the issue description contains several inaccuracies.**

#### Fact-checking the description

**1. ".env.example covers only ~5 of ~25+ available configuration options" — INACCURATE count.**
The `.env.example` on `main` has exactly **10 entries**: `HueApp__ClientId`, `HueApp__ClientSecret`, `HueApp__CallbackUrl`, `Email__FromAddress`, `Email__AwsRegion`, `Email__SendTimesUtc__0`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `ADMIN_PASSWORD_HASH`, and `Polling__IntervalMinutes`. Saying "~5" understates coverage by half.

The total count of distinct configurable options from `src/Hpoll.Core/Configuration/CustomerConfig.cs` (lines 14–58):
- `PollingSettings`: 7 properties (lines 29–35)
- `EmailSettings`: 9 properties (lines 38–46)
- `HueAppSettings`: 3 properties (lines 49–53)
- `BackupSettings`: 3 properties (lines 55–58)
- Plus: `DataPath`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `ADMIN_PASSWORD_HASH`, `PUID`/`PGID`, `DOTNET_ENVIRONMENT`/`ASPNETCORE_ENVIRONMENT`, Logging

That totals roughly **28-30** configurable values, so "~25+" is roughly correct. Of those, 10 are in `.env.example`, so **~18 are missing** — not ~20.

**2. "Health* settings" — FABRICATED.** `git grep -i health main` returns zero results across the entire codebase. No `HealthCheckIntervalSeconds`, no health endpoint, nothing. The `PollingSettings` class (lines 29–35 of `CustomerConfig.cs`) contains only: `IntervalMinutes`, `BatteryPollIntervalHours`, `DataRetentionHours`, `HttpTimeoutSeconds`, `TokenRefreshCheckHours`, `TokenRefreshThresholdHours`, `TokenRefreshMaxRetries`. No health-related settings.

**3. "All `Polling__*` settings" omitted — MOSTLY TRUE.** Only `Polling__IntervalMinutes` is in `.env.example`. The remaining 6 polling settings (`BatteryPollIntervalHours`, `DataRetentionHours`, `HttpTimeoutSeconds`, `TokenRefreshCheckHours`, `TokenRefreshThresholdHours`, `TokenRefreshMaxRetries`) are absent. However, all have sensible defaults in the class definition.

**4. "All `Email__*` advanced settings" omitted — TRUE.** `BatteryAlertThreshold` (default 60), `BatteryLevelCritical` (default 30), `BatteryLevelWarning` (default 50), `SummaryWindowHours` (default 4), `SummaryWindowCount` (default 7), and `ErrorRetryDelayMinutes` (default 5) are all missing from `.env.example`. Again, all have defaults.

**5. "AWS credential configuration guidance" — ALREADY PARTIALLY PRESENT.** The `.env.example` already has a comment `# AWS credentials for SES email delivery` with `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` entries. There is no IAM role guidance, but that is a deployment concern beyond the scope of `.env.example`.

**6. "PUID/PGID for docker-compose" — VALID.** `docker-compose.yml` (lines 4, 13) references `${PUID:-1000}:${PGID:-1000}` but these are absent from `.env.example`. Adding them with their defaults would be helpful.

**7. "Logging configuration" — LOW VALUE.** Logging defaults are in `appsettings.json` (both Worker and Admin). Overriding via env vars (e.g., `Logging__LogLevel__Default`) is technically possible but rarely done in `.env` files. This is a stretch.

**8. "ASPNETCORE_ENVIRONMENT" — ALREADY SET IN DOCKERFILES.** Both `Dockerfile` (line 40) and `Dockerfile.admin` (line 40) set `DOTNET_ENVIRONMENT=Production`. Adding it to `.env.example` is of marginal value since it would only help non-Docker local development.

**9. "Backup__*" settings entirely omitted from the issue — OVERSIGHT.** The issue fails to mention `BackupSettings` (`IntervalHours`, `RetentionCount`, `SubDirectory`) defined at lines 55-58 of `CustomerConfig.cs` and present in `appsettings.json`. These 3 settings are also missing from `.env.example`.

**10. Validation ranges concern — VALID BUT MISPLACED.** The issue correctly notes edge cases: `IntervalMinutes=0`, `DataRetentionHours < SummaryWindowHours * SummaryWindowCount`, `BatteryLevelCritical > BatteryLevelWarning`. The test file `tests/Hpoll.Core.Tests/ConfigurationValidationTests.cs` already tests some of these (e.g., `SummaryWindowHours=0` causes `DivideByZeroException` at line 37, and the test at line 71 verifies `BatteryLevelCritical <= BatteryLevelWarning`). However, these are runtime validation issues, not `.env.example` documentation issues — they should be tracked as a separate code issue, not bundled here.

#### Assessment of the previous review

The previous review's key claims are mostly accurate: Health* is fabricated, .env.example has 10 entries not ~5, AWS credential guidance is partially present. Its recommendation to separate validation ranges is sound.

#### Overall assessment

The core request — adding commented-out optional settings with their defaults to `.env.example` — is reasonable and would improve discoverability. The actual gap is **~18 missing settings**, not "~20 of ~25+". Priority of **low** is appropriate: all missing settings have working defaults, and the appsettings.json files serve as an alternative reference. The issue should be updated to:
- Remove the fabricated Health* reference
- Correct the count from "~5" to 10
- Add the overlooked `Backup__*` settings to the list of omissions
- Split validation concerns into a separate issue
