---
id: 111
title: "Consider composite index on DeviceReading for ReadingType filtering"
status: closed
closed: 2026-03-03
created: 2026-03-02
author: claude
labels: [enhancement, performance]
priority: low
---

## Description

The `DeviceReading` entity has indexes on `{DeviceId, Timestamp}` and `{Timestamp}`, but some queries also filter by `ReadingType`. Adding `ReadingType` to the index could allow SQLite to skip non-matching rows during index scans.

**Location:** `src/Hpoll.Data/HpollDbContext.cs` lines 43-51

**Affected queries:**
- `EmailRenderer.RenderDailySummaryAsync` — filters by motion, temperature, and battery reading types
- `DetailModel.LoadActivitySummaryAsync` — same pattern

**Current indexes:**
```csharp
entity.HasIndex(r => new { r.DeviceId, r.Timestamp });
entity.HasIndex(r => r.Timestamp);
```

**Limited practical benefit:** Because each Hue device exclusively produces one reading type (motion sensors produce motion readings, temperature sensors produce temperature readings), the `ReadingType` filter has near-100% selectivity after the `DeviceId` index narrows results. The existing `{DeviceId, Timestamp}` index already serves these queries efficiently for this device model.

**Recommendation:**
Low priority. If `{DeviceId, ReadingType, Timestamp}` is added, it can replace (not augment) the existing `{DeviceId, Timestamp}` index since it is a strict superset for all read queries. The more impactful performance improvement would be to push the battery query's "latest per device" aggregation into SQL rather than fetching all historical battery readings into memory (EmailRenderer lines 135-138).

## Comments

### claude — 2026-03-03

Fixed the actionable part: pushed battery query's 'latest per device' aggregation into SQL using a correlated subquery (WHERE Timestamp = MAX(Timestamp) per DeviceId), eliminating in-memory GroupBy/OrderByDescending/First. Now fetches 1 row per device instead of all battery readings from 7 days.
