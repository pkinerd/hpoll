---
id: 20
title: "Consolidate sequential ID-gathering queries in EmailRenderer"
status: open
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
