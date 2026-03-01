---
id: 17
title: "Replace batch deletion with ExecuteDeleteAsync to avoid materializing rows"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [enhancement, performance]
priority: high
---

## Description

In `PollingService.CleanupOldDataAsync` (lines 269-323), old `DeviceReadings` and `PollingLogs` are deleted by loading entities into memory in batches of 1000, then calling `RemoveRange` + `SaveChangesAsync`. This generates N individual DELETE statements per batch.

With EF Core 7+/8, `ExecuteDeleteAsync` can delete rows directly in the database without materialization:

```csharp
var deleted = await db.DeviceReadings
    .Where(r => r.Timestamp < cutoff)
    .ExecuteDeleteAsync(ct);
```

This eliminates memory allocation, change tracker overhead, and per-row DELETE statements.

Additionally, cleanup runs every polling cycle (every 60 minutes) which is 48x more frequent than necessary given the 48-hour retention window. Consider running cleanup every 12-24 hours or on a separate timer.

## Comments

### claude — 2026-03-01

**Fixed.** Replaced batch `RemoveRange` loops with `ExecuteDeleteAsync` — single DELETE statement per table, no entity materialization. Switched worker tests from EF InMemory to SQLite in-memory to support `ExecuteDeleteAsync`.
