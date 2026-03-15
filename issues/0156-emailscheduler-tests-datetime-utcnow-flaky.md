---
id: 156
title: "EmailSchedulerServiceTests use DateTime.UtcNow instead of mock TimeProvider"
status: open
created: 2026-03-15
author: claude
labels: [testing, code-quality]
priority: low
---

## Description

Several tests in `tests/Hpoll.Worker.Tests/EmailSchedulerServiceTests.cs` use `DateTime.UtcNow` for seeding test data and assertions rather than a mock `TimeProvider`. Other test files in the same project (e.g., `DatabaseBackupServiceTests`, `SystemInfoServiceTests`) use `FakeTimeProvider` or `Mock<TimeProvider>` for deterministic time control.

**Examples of patterns that could be improved:**
- `DateTime.UtcNow.AddMinutes(-5)` for setting past-due send times
- `Assert.True(customer.NextSendTimeUtc > DateTime.UtcNow)` for verification

The practical flakiness risk is low since the time margins used (5+ minutes) are large enough that system clock jitter is unlikely to cause failures. However, this is a code quality inconsistency — the service already accepts a `TimeProvider` parameter, and two tests in the file already use `Mock<TimeProvider>` (lines ~529-566 and ~588-631), so the pattern is partially adopted.

**Fix:** Update the `CreateService` helper to accept and inject a mock `TimeProvider` by default, and replace `DateTime.UtcNow` references in test data setup and assertions with values derived from the mock time provider.

**Related:** Issue #33 (closed) fixed timing-dependent tests in the Worker test suite. Issue #76 (closed) addressed `DateTime.UtcNow` vs `TimeProvider` inconsistency in production code. This issue covers the test-side counterpart.

**Source:** Comprehensive review (unit testing review), 2026-03-15

## Comments
