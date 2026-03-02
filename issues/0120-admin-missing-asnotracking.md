---
id: 120
title: "Missing AsNoTracking on read-only Admin queries wastes change-tracker memory"
status: open
created: 2026-03-02
author: claude
labels: [enhancement, performance]
priority: low
---

## Description

Several Admin page queries that are read-only (used only for display) do not use `.AsNoTracking()`. This causes EF Core to track all returned entities in the change tracker, consuming extra memory for no benefit since these entities are never modified.

**Affected locations:**
- `src/Hpoll.Admin/Pages/Index.cshtml.cs` lines 34-51: three `ToListAsync()` queries (ExpiringTokenHubs, FailingHubs, RecentLogs). Note: the five `CountAsync()` calls on lines 26-31 return scalars and do not track entities — they are not affected.
- `src/Hpoll.Admin/Pages/Customers/Index.cshtml.cs` lines 18-21: customer list query
- `src/Hpoll.Admin/Pages/About.cshtml.cs` lines 39-42: system info query
- `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs` lines 171-187: `LoadHub` method loads Hub, Devices, and RecentLogs purely for display

**Note:** The DbContext is scoped per-request and disposed at request end, so the practical memory impact is minimal for this low-concurrency admin portal. This is a best-practice improvement rather than a fix for an observed problem.

**Category:** performance
**Severity:** low
**Found by:** Code quality review (comprehensive review 2026-03-02)

### Recommendation

Add `.AsNoTracking()` to all queries in the Admin portal that are used only for display. This is a straightforward change with no behavioral impact — it simply tells EF Core not to track the returned entities. Alternatively, consider configuring `QueryTrackingBehavior.NoTracking` globally for the Admin portal's DbContext registration, since the portal predominantly reads data.

## Comments
