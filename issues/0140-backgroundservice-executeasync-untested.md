---
id: 140
title: "PollingService.ExecuteAsync has no test coverage; other services have partial error-path gaps"
status: closed
closed: 2026-03-15
created: 2026-03-04
author: claude
labels: [testing]
priority: medium
---

## Description

**PollingService.ExecuteAsync** (lines 47-85) is genuinely untested — no test calls `StartAsync`/`StopAsync`. All tests invoke `PollAllHubsAsync` and `CleanupOldDataAsync` directly, skipping the orchestration loop entirely.

The other three services **do** have lifecycle tests via `StartAsync`/`StopAsync`:
- **EmailSchedulerService** — `GracefulShutdown_StopsWithoutException`, `InvalidSendTimes_DefaultsStartsWithoutException`. Gap: error-retry delay path (`ErrorRetryDelayMinutes`) and `do/while` re-check loop are untested.
- **TokenRefreshService** — `ExecuteAsync_StopsGracefullyOnCancellation`, `ExecuteAsync_UpdatesSystemInfoMetrics`, `ExecuteAsync_SystemInfoFailure_DoesNotCrashService`. Gap: the generic `Exception` catch in the outer loop may not be directly exercised.
- **DatabaseBackupService** — `ExecuteAsync_NoExistingBackups_CreatesInitialBackup`, `ExecuteAsync_SystemInfoFailure_DoesNotCrashService`, etc. Gaps: some error catches in `RunBackupCycleAsync`.

**Recommendation:** Add lifecycle tests for `PollingService.ExecuteAsync` using `StartAsync`/`StopAsync` with `CancellationTokenSource`. For the other services, consider adding targeted error-path tests for the specific gaps noted above.

**See also:** #87 (closed 2026-03-03) covered PollingService.ExecuteAsync specifically. This issue re-verified the finding and adds context about the other services' partial gaps.

**Found by:** Comprehensive review — unit testing + code coverage reviews.

## Comments

### critical-review — 2026-03-04

Critical review: ADJUST. Retitled and lowered priority from high to medium. Only PollingService.ExecuteAsync is genuinely untested. EmailSchedulerService, TokenRefreshService, and DatabaseBackupService all have lifecycle tests via StartAsync/StopAsync. Gaps remain only in specific error-retry paths.

### claude — 2026-03-15

Duplicate of closed issues #87 (PollingService.ExecuteAsync), #71 (EmailSchedulerService error-retry), #102 (TokenRefreshService outer catch), #91 (DatabaseBackupService error paths). All were closed 2026-03-03 as won't-fix. This issue was created one day later and describes the same gaps with no new findings.
