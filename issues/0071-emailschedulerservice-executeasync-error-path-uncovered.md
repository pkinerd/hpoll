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

### claude — 2026-03-01 (detailed review)

**Verdict: VALID but LOW priority. Recommend closing as won't-fix.**

**Factual accuracy of the issue:**

1. **Line numbers are wrong.** The issue says lines 50-60, but the actual error/retry path is at lines 67-78 (the `catch (Exception ex)` block at line 67, the `Task.Delay` retry at line 72, and the inner `OperationCanceledException` catch at lines 74-77). The previous review comment already noted this.

2. **Method name is fabricated.** The issue references `SendAllEmailsAsync` which does not exist anywhere in the codebase. The actual methods are `ProcessDueCustomersAsync` and `SendCustomerEmailAsync`.

3. **The 0% coverage claim for this specific path is plausible.** No test in `EmailSchedulerServiceTests.cs` forces the `ExecuteAsync` outer catch block to execute. The existing `ProcessDueCustomers_ContinuesOnSingleCustomerFailure` test covers the *per-customer* try/catch inside `ProcessDueCustomersAsync` (around line 108), not the top-level `ExecuteAsync` catch. The `GracefulShutdown_StopsWithoutException` and `InvalidSendTimes_DefaultsStartsWithoutException` tests exercise `StartAsync`/`StopAsync` but do not trigger the error retry path.

4. **The coverage percentages (91.5% line, 68.4% for ExecuteAsync) look reasonable** given that the tested internal methods (`ProcessDueCustomersAsync`, `InitializeNextSendTimesAsync`, `GetSleepDurationAsync`, `ParseEmailList`, `SendCustomerEmailAsync`) are well-covered, but `ExecuteAsync` itself is only partially reached via `StartAsync`/`StopAsync`.

**Assessment of the error path's complexity:**

The untested code (lines 67-78) is:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Unhandled error in email scheduler");
    try
    {
        await Task.Delay(TimeSpan.FromMinutes(_settings.ErrorRetryDelayMinutes), stoppingToken);
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
        break;
    }
}
```

This is a **standard catch-log-delay-continue pattern** with zero branching logic beyond the cancellation guard. There is no conditional retry logic, no backoff, no state mutation, no error classification, and no recovery action. The only configurable value is `ErrorRetryDelayMinutes` (default: 5), which is a simple `TimeSpan.FromMinutes()` pass-through.

**Assessment of the recommended testing approach:**

The issue recommends injecting a mock `IEmailRenderer` that throws, then verifying the error is logged, the service survives, and `ErrorRetryDelayMinutes` is respected. This is **technically feasible** -- you could make `ProcessDueCustomersAsync` throw by having the `IServiceScopeFactory`-created DB context throw, or by seeding data that triggers an exception in the do/while loop. However:

- **Testing requires running the full `ExecuteAsync` loop** via `StartAsync`, which brings timer-based async coordination, `Task.Delay` waits, and `CancellationToken` gymnastics. The commit f142a28 deliberately moved away from `StartAsync`/`StopAsync`-based testing toward calling internal methods directly to eliminate flakiness.
- **Verifying `ErrorRetryDelayMinutes` is respected** would require either injecting `TimeProvider` into the `Task.Delay` call (which the code does not do -- it uses `Task.Delay` directly, not `_timeProvider`), or waiting for the actual delay and measuring elapsed time, which is inherently flaky.
- **The ROI is very low.** The code under test is identical in structure to the equivalent paths in `TokenRefreshService` (lines 65-68) and `PollingService` (lines 70-73), which were explicitly left untested when those issues (#32, #36) were closed in commit f142a28. Testing this one instance while leaving the other two untested would be inconsistent.

**Recommendation:** Close as won't-fix. The error path is trivial boilerplate with no branching logic. The testing approach would reintroduce the `StartAsync`/`StopAsync` timing-dependent test style that was deliberately removed. All three BackgroundService `ExecuteAsync` error paths follow the same pattern and were left untested as a deliberate project decision. If the pattern ever grows more complex (e.g., exponential backoff, circuit breaker, error classification), testing would become warranted at that point.
