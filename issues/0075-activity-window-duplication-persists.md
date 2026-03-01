---
id: 75
title: "Activity window duplication persists — ActivitySummaryBuilder never extracted"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, code-quality]
priority: high
---

## Description

Issue #9 was closed but the `ActivitySummaryBuilder` service was never created. The activity window
aggregation logic (~80-100 lines) remains duplicated nearly identically between:

- `src/Hpoll.Email/EmailRenderer.cs` (lines 27-178) — `RenderDailySummaryAsync`
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs` (lines 272-358) — `LoadActivitySummaryAsync`

Both perform identical: time window bucketing, hub/device/reading DB queries, JSON parsing of
motion/temperature readings, and summary construction with the same fields. The `WindowSummary`
class in EmailRenderer and `ActivityWindow` class in Detail are structurally identical.

Additionally, the JSON parsing lambdas for motion detection (checking `motion` boolean from
JSON) are duplicated within each file and across files.

**Found by:** Comprehensive review — code quality review.

**Recommendation:** Extract a shared `ActivitySummaryBuilder` service into `Hpoll.Core` as
originally planned in #9. Both `EmailRenderer` and `Detail.cshtml.cs` should call this
shared service.

## Comments
