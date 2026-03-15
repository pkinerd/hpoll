---
id: 162
title: "TokenRefreshServiceTests use DateTime.UtcNow instead of FakeTimeProvider"
status: closed
created: 2026-03-15
author: claude
labels: [testing, code-quality]
priority: low
closed: 2026-03-15
---

## Description

`tests/Hpoll.Worker.Tests/TokenRefreshServiceTests.cs` uses `DateTime.UtcNow` for seeding token expiry values (e.g., `DateTime.UtcNow.AddDays(1)`) rather than using `FakeTimeProvider`. The `CreateService` method does not inject a `TimeProvider` at all, even though `TokenRefreshService` already accepts an optional `TimeProvider` parameter.

**Impact:**
- Tests use large time margins (AddDays, AddHours) so practical flakiness risk is low
- Timestamp assertions use weak patterns like `Assert.True(updatedHub.UpdatedAt >= beforeRefresh)` that could be exact with FakeTimeProvider
- Inconsistent with `PollingServiceTests` and `EmailSchedulerServiceTests` which both use `FakeTimeProvider`

Related issues: #33 (closed, introduced TimeProvider), #103 (closed, fixed boundary test assertion), #156 (closed, same finding for EmailSchedulerServiceTests).

**Recommendation:** Inject a `FakeTimeProvider` into `TokenRefreshService` via `CreateService` and seed all times relative to it. This eliminates timing fragility and enables exact timestamp assertions, matching the pattern already used in `EmailSchedulerServiceTests`.
