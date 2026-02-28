---
id: 22
title: "Add standalone Timestamp index on PollingLog for cleanup queries"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, performance]
priority: medium
---

## Description

`PollingLog` has a composite index on `{HubId, Timestamp}` but no standalone index on `Timestamp`. The cleanup query in `PollingService.CleanupOldDataAsync` (line 299) filters only by `Timestamp`:

```csharp
var batch = await db.PollingLogs
    .Where(l => l.Timestamp < cutoff)
    .Take(batchSize)
    .ToListAsync(ct);
```

Since `HubId` is the leading column in the composite index, SQLite cannot efficiently use it for timestamp-only filtering.

**Fix:** Add a standalone index on `PollingLog.Timestamp` via a new migration.

## Comments
