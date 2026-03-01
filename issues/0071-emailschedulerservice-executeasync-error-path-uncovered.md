---
id: 71
title: "EmailSchedulerService.ExecuteAsync error retry path has 0% test coverage"
status: open
created: 2026-03-01
author: claude
labels: [testing]
priority: low
---

## Description

Lines 50-60 of `EmailSchedulerService.ExecuteAsync` have 0% code coverage. This includes:
- The generic `Exception` catch block that handles unhandled errors (line 50-56)
- The error retry delay via `ErrorRetryDelayMinutes` (line 54)
- The inner `OperationCanceledException` handler during the retry delay (lines 58-60)

The `ParseEmailList` helper and `SendAllEmailsAsync` methods have full coverage, but the main service loop's error resilience is untested.

**Location:** `src/Hpoll.Worker/Services/EmailSchedulerService.cs` lines 50-60

**Coverage data:** File overall 91.5% line / 90.5% branch; `ExecuteAsync` method at 68.4%

**Recommendation:** Add tests that:
1. Inject a mock `IEmailRenderer` that throws an unexpected exception
2. Verify the error is logged
3. Verify the service continues running (doesn't crash) after the retry delay
4. Verify the `ErrorRetryDelayMinutes` setting is respected

This follows the same gap pattern as `TokenRefreshService.ExecuteAsync` (issue #0032) and `PollingService.ExecuteAsync` (issue #0036).

*Found during comprehensive review (code coverage analysis).*

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. Line numbers wrong (67-78, not 50-60). SendAllEmailsAsync **does not exist** (actual: SendCustomerEmailAsync, ProcessDueCustomersAsync). Issues #0032/#0036 were closed without testing their equivalent ExecuteAsync error paths either (deliberate choice per commit f142a28). The error path is 11 lines of standard BackgroundService boilerplate.
