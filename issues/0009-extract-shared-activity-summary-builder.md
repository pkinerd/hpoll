---
id: 9
title: "Extract shared ActivitySummaryBuilder to eliminate duplicated window aggregation logic"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: high
---

## Description

The activity summary / window aggregation logic (~80 lines) is nearly identical between two files:

- `src/Hpoll.Email/EmailRenderer.cs` (lines 26-157) — `RenderDailySummaryAsync`
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs` (lines 169-251) — `LoadActivitySummaryAsync`

Both perform identical: time window bucketing, hub/device/reading DB queries, JSON parsing of motion/temperature readings, and `WindowSummary`/`ActivityWindow` construction with the same 7 fields.

Additionally, the JSON parsing lambdas for motion detection are duplicated **within** each file (parsing each reading twice — once for `devicesWithMotion`, once for `totalMotionEvents`), resulting in 14 JSON parses per email render where 7 would suffice.

**Recommendation:** Extract a shared `ActivitySummaryBuilder` service into `Hpoll.Core` that takes `customerId`, `timeZoneId`, and `nowUtc`, returning a list of summary window objects. Both `EmailRenderer` and `Detail.cshtml.cs` should call this shared service. The `WindowSummary` / `ActivityWindow` model should also live in `Hpoll.Core.Models`.

## Comments
