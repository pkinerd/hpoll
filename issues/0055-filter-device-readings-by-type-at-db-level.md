---
id: 55
title: "Filter DeviceReadings by ReadingType at database level in activity summary queries"
status: closed
created: 2026-03-01
author: claude
labels: [enhancement, performance]
priority: medium
---

## Description

In `Customers/Detail.cshtml.cs` (line 198-201), the activity summary query loads ALL `DeviceReading` rows for the given devices and time range into memory, then filters by `ReadingType` in-memory. This pulls unnecessary reading types (e.g., battery readings) and all raw JSON `Value` strings into memory.

```csharp
var readings = await _db.DeviceReadings
    .Where(r => deviceIds.Contains(r.DeviceId) && r.Timestamp >= startUtc && r.Timestamp < nowUtc)
    .ToListAsync();
// Then later filters: readings.Where(r => r.ReadingType == "motion")
```

**File:** `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs:198-201`

**Recommended fix:** Add a `ReadingType` filter to the database query:

```csharp
var readings = await _db.DeviceReadings
    .Where(r => deviceIds.Contains(r.DeviceId)
        && r.Timestamp >= startUtc && r.Timestamp < nowUtc
        && (r.ReadingType == "motion" || r.ReadingType == "temperature"))
    .ToListAsync();
```

This also applies to `EmailRenderer.cs` which has the same pattern. Both should be addressed when extracting the shared `ActivitySummaryBuilder` (issue #0009).

**Source:** Efficiency review finding E3

## Comments

### claude — 2026-03-01

**Closed:** Consolidated into #0020 (Consolidate sequential ID-gathering queries in EmailRenderer). The `ReadingType` filter is now part of #0020's consolidated query — a natural addition when rewriting the query to use navigation properties.
