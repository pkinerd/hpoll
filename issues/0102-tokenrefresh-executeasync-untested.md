---
id: 102
title: "TokenRefreshService.ExecuteAsync outer exception catch untested"
status: closed
closed: 2026-03-03
created: 2026-03-02
author: claude
labels: [testing]
priority: low
---

## Description

Three `ExecuteAsync` tests already exist in the test suite:
- `ExecuteAsync_StopsGracefullyOnCancellation` — covers cancellation/shutdown (lines 63-66)
- `ExecuteAsync_UpdatesSystemInfoMetrics` — covers happy path metrics
- `ExecuteAsync_SystemInfoFailure_DoesNotCrashService` — covers inner system info exception

The only genuinely untested path is **lines 67-70**: the outer `catch (Exception ex)` block that catches unhandled errors from `RefreshExpiringTokensAsync` itself. This is a 4-line standard catch-log-continue pattern with no branching logic.

**Location:** `src/Hpoll.Worker/Services/TokenRefreshService.cs`, lines 67-70

**Note:** The identical pattern in sibling services (`PollingService`, `EmailSchedulerService`, `DatabaseBackupService`) was deliberately left untested as a project decision. This issue is tracked for consistency with #0087 and #0071.

**Recommendation:**
If testing this path, configure `_mockHueApiClient` to throw a non-`OperationCanceledException` from within `RefreshExpiringTokensAsync`, then verify the service continues running. However, this is 4 lines of standard boilerplate and the practical risk is minimal.

## Comments

### claude — 2026-03-02

Skipping: Same ExecuteAsync outer catch pattern as #0071. TokenRefreshService error handling is identical boilerplate to EmailSchedulerService and PollingService, which were all deliberately left untested.

### claude — 2026-03-03

Closing as won't-fix. The outer `catch (Exception ex)` block (lines 71-74) is a 4-line standard catch-log-continue pattern identical to the sibling services closed in #0071, #0087, and #0091. Three ExecuteAsync tests already exist covering cancellation, system info metrics, and system info failure. The only gap is this trivial error handler with no branching logic.
