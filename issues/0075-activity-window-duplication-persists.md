---
id: 75
title: "Activity window duplication persists — ActivitySummaryBuilder never extracted"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, code-quality]
priority: low
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

### claude — 2026-03-01

**Critical review: multiple factual inaccuracies and flawed reasoning identified. Priority downgraded from high to low.**

This issue contains several errors that overstate the severity and scope of the duplication:

#### 1. Misleading framing of #9 closure

Issue #9 was not forgotten or accidentally closed — it was **consciously deferred** with explicit reasoning. The closing comment on #9 states: *"The duplication, while not ideal, is stable and well-tested. Deferring until there's a stronger driver for the refactor."* This issue presents the deferral as an oversight ("never extracted") rather than an intentional decision. Reopening a deferred item requires a new justification, which is not provided here.

#### 2. "Structurally identical" classes claim is factually wrong

`WindowSummary` (EmailRenderer, line 311) has **9 properties**:
- `Label`, `WindowStartLocal`, `DisplayEndLocal`, `DevicesWithMotion`, `TotalMotionSensors`, `TotalMotionEvents`, `TemperatureMin`, `TemperatureMedian`, `TemperatureMax`

`ActivityWindow` (Detail.cshtml.cs, line 360) has **7 properties**:
- `Label`, `DevicesWithMotion`, `TotalMotionSensors`, `TotalMotionEvents`, `TemperatureMin`, `TemperatureMedian`, `TemperatureMax`

`WindowSummary` has two additional fields (`WindowStartLocal`, `DisplayEndLocal`) that are used by EmailRenderer for incomplete-window omission logic (line 169) and the `FormatLabelHtml` helper (line 303). These are not present in `ActivityWindow`. The classes are **not** structurally identical.

#### 3. Line range for EmailRenderer overstates duplicated scope

The issue claims lines 27-178 are duplicated. That range covers the **entire** `RenderDailySummaryAsync` method, but lines 131-178 contain logic that has **no counterpart** in `LoadActivitySummaryAsync`:

- **Battery readings** (lines 134-164): ~30 lines querying, parsing, and building battery statuses — entirely absent from Detail.
- **Incomplete window omission** (lines 168-175): Omits newest window if < 60 minutes of data — absent from Detail.
- **Timezone abbreviation formatting** (line 132): Absent from Detail.
- **BuildHtml call** (line 178): The entire HTML rendering pipeline — absent from Detail.

The actually-duplicated core is roughly **lines 29-129** (~100 lines in EmailRenderer, ~83 lines in Detail), not the 151-line range claimed.

#### 4. "Both perform identical" understates meaningful behavioral differences

Several differences make extraction less straightforward than the issue implies:

- **CancellationToken**: EmailRenderer propagates `CancellationToken ct` through all DB queries; Detail does not accept or propagate one (see also #73).
- **Injectable time**: EmailRenderer accepts an optional `DateTime? nowUtc` parameter (enabling testability); Detail hardcodes `DateTime.UtcNow`.
- **Query end time**: EmailRenderer uses a computed `endUtc` as the upper bound for reading queries (line 59); Detail uses `nowUtc` directly (line 303). These differ when `endUtc != nowUtc`.
- **Logging**: EmailRenderer logs when no readings are found (lines 64-69); Detail does not.
- **Motion sensor count query ordering**: EmailRenderer queries motion sensor count after readings (line 72); Detail queries it before readings (line 297).

These are behavioral differences, not cosmetic ones. A shared service would need to accommodate all of them, adding parameters or configuration to handle each variant.

#### 5. Priority "high" is not justified

The original #9 was deferred because the duplication is "stable and well-tested." This issue provides no new evidence of bugs, drift between the copies, or a concrete business driver. The JSON parsing duplication *within* each file (motion parsed twice per window) was already identified in #56 and consolidated into #9's scope. No new information is presented.

**Revised assessment:** The core duplication (~80-100 lines of window bucketing, DB queries, and JSON parsing) is real but overstated in scope and urgency. Priority downgraded to **low** — this is a genuine but non-urgent code quality improvement that should only be pursued when there's a concrete driver (e.g., needing to change the aggregation logic and facing the risk of the two copies diverging).
