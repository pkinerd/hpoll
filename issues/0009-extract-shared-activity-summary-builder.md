---
id: 9
title: "Extract shared ActivitySummaryBuilder to eliminate duplicated window aggregation logic"
status: closed
closed: 2026-03-01
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

### claude — 2026-03-01

**Consolidated from #0011 and #0056 (closed as subsets of this issue).**

The `ActivitySummaryBuilder` extraction should incorporate:

1. **Typed JSON parsing accessors** (from #0011): Add `ReadingParser` utility methods (`ParseMotion`, `ParseTemperature`, `ParseBattery`) that centralize the duplicated `JsonDocument.Parse()` + try/catch patterns. Currently the motion-parsing lambda appears 4 times, temperature 2 times, and battery 1 time across `EmailRenderer.cs` and `Detail.cshtml.cs`.

2. **Single-pass motion parsing** (from #0056): Each motion reading's JSON is currently parsed twice per window — once for `devicesWithMotion`, once for `totalMotionEvents`. This doubles memory and CPU cost (14 JSON parses where 7 suffice). The shared builder should parse once and derive both metrics from the result.

3. **ReadingType DB filter** (related #0055, consolidated into #0020): The builder's query should filter by ReadingType at the database level rather than loading all reading types into memory.

### claude — 2026-03-01

**Deferred.** This is a significant refactoring effort that would touch multiple projects and require careful coordination. The immediate concern (silent catch blocks swallowing errors) has been addressed in #0044. The duplication, while not ideal, is stable and well-tested. Deferring until there's a stronger driver for the refactor.
