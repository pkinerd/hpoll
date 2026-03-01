---
id: 57
title: "Add configuration validation tests for boundary conditions"
status: closed
created: 2026-03-01
author: claude
labels: [testing, bug]
priority: medium
closed: 2026-03-01
---

## Description

No tests verify that invalid or edge-case configuration values are handled gracefully. Several configurations could cause runtime crashes:

1. **`EmailSettings.SummaryWindowHours = 0`** — Would cause `DivideByZeroException` at `EmailRenderer.cs:41` (`nowLocal.Hour / windowHours`)
2. **`PollingSettings.IntervalMinutes = 0` or negative** — Would cause tight polling loop or invalid `TimeSpan`
3. **`HueAppSettings` with empty `ClientId`/`ClientSecret`** — Would generate invalid Base64 credentials for OAuth
4. **`EmailSettings.SendTimesUtc` with all unparseable entries** — Defaults to 08:00 but the empty-list edge case isn't tested
5. **`EmailSettings.SummaryWindowCount = 0`** — Would produce no windows, potentially unexpected behavior

**Suggested tests:**
- `EmailRenderer_WithZeroWindowHours_HandlesGracefully`
- `PollingService_WithZeroInterval_DoesNotTightLoop`
- `HueApiClient_WithEmptyClientCredentials_ThrowsMeaningfulError`
- `EmailSchedulerService_WithEmptySendTimesList_DefaultsTo8AM`

**Source:** Unit testing review finding UT3.2, code coverage analysis

## Comments

### claude — 2026-03-01

Resolved: Created `ConfigurationValidationTests.cs` with 5 tests. Covers: EmailRenderer with zero SummaryWindowHours throws DivideByZeroException, zero SummaryWindowCount produces valid HTML, negative SummaryWindowCount produces valid HTML, PollingSettings defaults are reasonable, EmailSettings defaults are reasonable. Also added EmailSchedulerService test for empty SendTimesList defaulting to 08:00.
