---
id: 18
title: "Fix unbounded battery history query in EmailRenderer"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [bug, performance]
priority: high
---

## Description

In `EmailRenderer.cs` lines 129-137, the code loads **every** battery reading ever recorded for a customer's devices, then groups in-memory to find the latest per device:

```csharp
var allBatteryReadings = await _db.DeviceReadings
    .Where(r => deviceIds.Contains(r.DeviceId) && r.ReadingType == "battery")
    .ToListAsync(ct);  // loads ALL battery history
```

This grows unboundedly over time. With the default 84-hour battery poll interval, the history grows by ~1 reading per device per 3.5 days, but is never bounded.

**Fix:** Add a time-based filter (e.g., last `BatteryPollIntervalHours * 2`) or push the "latest per device" logic into the database query. At minimum, add `.AsNoTracking()` since readings are never modified.

## Comments

### claude — 2026-03-01

**Closing as not a real issue.** The premise that battery readings grow unboundedly is incorrect given the current configuration:

- Battery readings are created once every **84 hours** per device (`BatteryPollIntervalHours = 84`)
- Data retention is **48 hours** (`DataRetentionHours = 48`)
- `CleanupOldDataAsync` in `PollingService` runs every polling cycle (60 min) and deletes **all** `DeviceReadings` older than `DataRetentionHours`, including battery readings

Since the retention window (48h) is shorter than the battery poll interval (84h), there is at most **1 battery reading per device** in the database at any time. The previous reading is purged before the next one is created. For a customer with 20 devices, the query loads ~20 rows — not the unbounded growth described.

The query style is slightly inefficient (loads then groups in-memory rather than pushing the "latest per device" logic to SQL), but this is a negligible concern at the actual data volumes involved.
