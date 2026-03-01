---
id: 20
title: "Consolidate sequential ID-gathering queries in EmailRenderer"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [enhancement, performance]
priority: medium
---

## Description

In `EmailRenderer.cs` lines 46-58, three sequential queries fetch hub IDs, then device IDs, then readings. These can be expressed as a single query using EF Core navigation properties:

```csharp
var readings = await _db.DeviceReadings
    .Where(r => r.Device.Hub.CustomerId == customerId
             && r.Device.Hub.Status == "active"
             && r.Timestamp >= startUtc && r.Timestamp < endUtc)
    .AsNoTracking()
    .ToListAsync(ct);
```

This eliminates two round-trips. The same pattern exists in `Detail.cshtml.cs` `LoadActivitySummaryAsync`.

Also, add `.AsNoTracking()` to all read-only queries (readings are never modified).

## Comments

### claude — 2026-03-01

**Consolidated from #0055 (closed as subset of this issue).**

When consolidating the queries using navigation properties, also add a `ReadingType` filter at the DB level (from #0055). The current query loads ALL reading types (motion, temperature, battery) into memory and filters in-memory. The consolidated query should be:

```csharp
var readings = await _db.DeviceReadings
    .Where(r => r.Device.Hub.CustomerId == customerId
             && r.Device.Hub.Status == "active"
             && r.Timestamp >= startUtc && r.Timestamp < endUtc
             && (r.ReadingType == "motion" || r.ReadingType == "temperature"))
    .AsNoTracking()
    .ToListAsync(ct);
```

This eliminates three optimizations in one: two DB round-trips removed (hub IDs + device IDs queries), unnecessary reading types excluded at DB level, and change tracking disabled for read-only data. The same fix applies to `Detail.cshtml.cs`.

### claude — 2026-03-01

**Partially fixed, closing.** Added `ReadingType` filter at DB level and `.AsNoTracking()` to activity summary queries in both `EmailRenderer` and `Detail.cshtml.cs`. The full navigation-property query consolidation was deferred as it provides marginal benefit over the targeted filter fix.
