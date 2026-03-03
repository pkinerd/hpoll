---
id: 91
title: "DatabaseBackupService error handling paths untested"
status: closed
closed: 2026-03-03
created: 2026-03-02
author: claude
labels: [testing]
priority: low
---

## Description

`DatabaseBackupService` has 85.2% line coverage (92/108 lines) and 95% branch coverage (19/20). The uncovered code is exclusively in defensive error handling paths that log and continue — none affect data integrity:

1. **`ExecuteAsync` cancellation handling** (lines 64-70): The `Task.Delay` cancellation catch and delayed `RunBackupCycleAsync` call (trivial break-from-loop path)
2. **`InitializeStatsFromExistingBackupsAsync` error handler** (lines 101-104): Catch block for stat initialization failures
3. **`RunBackupCycleAsync` error handlers** (lines 134-141): Both the `OperationCanceledException` catch and the general `Exception` catch for `CreateBackupAsync` failures
4. **`PruneOldBackups` error handlers** (lines 190-203): Individual file deletion failure catch and overall pruning failure catch

All uncovered paths are purely defensive — they log the error and continue operation. The service is a background backup utility, not a critical transaction path.

**Recommended tests:**
- Simulate I/O failure during backup creation by corrupting the database path or making the backup directory unwritable before calling `CreateBackupAsync`
- Simulate file deletion failure during pruning by making the backup directory read-only (`chmod 555`) to force `File.Delete` to throw
- Verify that initialization stat failures are logged and do not crash the service (pre-create backup files but make `SetAsync` throw)

**Location:** `src/Hpoll.Worker/Services/DatabaseBackupService.cs:64-70, 101-104, 134-141, 190-203`

## Comments

### claude — 2026-03-02

Skipping: DatabaseBackupService error handling paths are catch-log-continue blocks identical to the pattern in #0071/#0087. These were deliberately left untested as a project design decision.

### claude — 2026-03-03

Closing as won't-fix. All uncovered paths are purely defensive catch-log-continue blocks identical to the pattern in #0071 and #0087. The service has 85.2% line coverage and 95% branch coverage — the gaps are exclusively in error handlers that log warnings and continue operation. These were deliberately left untested as a project design decision consistent with all BackgroundService implementations.
