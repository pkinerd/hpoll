---
id: 112
title: "DatabaseBackupService total backups counter produces meaningless hybrid value"
status: open
created: 2026-03-02
author: claude
labels: [bug, code-quality]
priority: low
---

## Description

The `_totalBackups` counter in `DatabaseBackupService` produces a meaningless hybrid value: it is initialized from the on-disk file count at startup, then incremented each cycle without accounting for pruning. After a restart, it is neither the true lifetime count nor the true current file count.

**Location:** `src/Hpoll.Worker/Services/DatabaseBackupService.cs` line 119

**Problem:**
- On startup with existing backups (line 82), `_totalBackups` is set to the actual file count (e.g., 7)
- Each backup cycle increments `_totalBackups++` (line 119) after pruning (line 118) removes old files
- After N cycles post-restart with retention of 7: metric shows `7 + N`, actual files remain 7
- On next restart, counter resets to 7 again — so it is not a reliable lifetime counter either
- The metric name `backup.total_backups` implies a current count, which it is not

**Recommendation:**
Recount actual files after pruning: `_totalBackups = Directory.GetFiles(backupDir, "*.db").Length;` and rename the metric to `backup.current_count` for clarity. This makes the metric genuinely useful as a "current backup file count" rather than a meaningless hybrid.

## Comments
