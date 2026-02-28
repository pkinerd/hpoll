---
id: 18
title: "Fix unbounded battery history query in EmailRenderer"
status: open
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
