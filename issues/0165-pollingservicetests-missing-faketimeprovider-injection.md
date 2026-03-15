---
id: 165
title: "PollingServiceTests doesn't inject FakeTimeProvider by default"
status: closed
closed: 2026-03-15
created: 2026-03-15
author: claude
labels: [testing, code-quality]
priority: low
---

## Description

`PollingServiceTests` uses `DateTime.UtcNow` directly in test seed data and mock responses rather than obtaining the current time from a `FakeTimeProvider`. The `PollingService` constructor accepts an optional `TimeProvider? timeProvider = null` parameter, but the tests never supply one — so both the seed data and the service's internal time queries use real system time. This means there is no actual clock skew bug today, but the tests are not deterministic in principle and are inconsistent with the pattern established in `TokenRefreshServiceTests` (issue #162).

**Location:** `tests/Hpoll.Worker.Tests/PollingServiceTests.cs`

Specific `DateTime.UtcNow` usages to replace:
- `SeedHubAsync` — `TokenExpiresAt` uses `DateTime.UtcNow.AddDays(7)`
- `SetupSuccessfulHueResponses` — motion `Changed` timestamp uses `DateTime.UtcNow`
- `SetupSuccessfulHueResponses` — temperature `Changed` timestamp uses `DateTime.UtcNow`

**Category:** testing, consistency

**Severity:** low — tests are not currently flaky (real system time is used consistently), but the inconsistency could cause confusion and prevents future deterministic time control.

**Recommendation:** Adopt the same `FakeTimeProvider` pattern used in `TokenRefreshServiceTests`:
1. Add a `FakeTimeProvider _fakeTime` field initialized to a fixed date
2. Replace all `DateTime.UtcNow` references in `SeedHubAsync` and `SetupSuccessfulHueResponses` with `_fakeTime.GetUtcNow().UtcDateTime`
3. Pass `_fakeTime` to the `PollingService` constructor (it already accepts `TimeProvider? timeProvider = null`)

This aligns with the project's move toward deterministic time in tests (see issue #162).

## Comments

### claude — 2026-03-15

Fixed: Injected FakeTimeProvider into PollingServiceTests, replacing all 30 DateTime.UtcNow usages with deterministic time.
