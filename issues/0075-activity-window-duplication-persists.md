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

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

The core finding is real and well-identified, but some claims contain minor inaccuracies and the priority is overstated given the project history.

**Claim-by-claim verification:**

1. **"Issue #9 was closed but the ActivitySummaryBuilder service was never created"** — VERIFIED. Issue #9 exists with status `closed` and no `ActivitySummaryBuilder` class exists anywhere in the codebase. However, this is somewhat misleading: issue #9 was explicitly *deferred* (not completed), with a comment stating "The duplication, while not ideal, is stable and well-tested. Deferring until there's a stronger driver for the refactor." This issue re-raises a known, deliberately deferred item without acknowledging that context.

2. **Line numbers — EmailRenderer.cs lines 27-178 for `RenderDailySummaryAsync`** — ACCURATE. The method spans lines 27-179 (line 178 is the last statement before the closing brace).

3. **Line numbers — Detail.cshtml.cs lines 272-358 for `LoadActivitySummaryAsync`** — EXACTLY CORRECT.

4. **"~80-100 lines" of duplicated logic** — ACCURATE. `LoadActivitySummaryAsync` is 86 lines; the comparable window aggregation portion of `RenderDailySummaryAsync` is approximately 102 lines (lines 27-129, before the battery and HTML sections).

5. **"Both perform identical" operations** — VERIFIED in detail. The following are functionally identical across both methods: timezone conversion, window hour/count calculation, hub ID query (filtering by CustomerId + Active status), device ID query, motion sensor count query, readings query (filtered by deviceIds, time range, Motion/Temperature types, with AsNoTracking), the windowing loop (same bucket arithmetic), motion JSON parsing lambdas, temperature JSON parsing lambdas, summary object construction (same field calculations including First()/Count-div-2/Last() for temperatures), and the final `.Reverse()` call.

6. **"`WindowSummary` class in EmailRenderer and `ActivityWindow` class in Detail are structurally identical"** — PARTIALLY INACCURATE. `WindowSummary` (EmailRenderer.cs lines 311-322) has 9 properties: Label, WindowStartLocal, DisplayEndLocal, DevicesWithMotion, TotalMotionSensors, TotalMotionEvents, TemperatureMin, TemperatureMedian, TemperatureMax. `ActivityWindow` (Detail.cshtml.cs lines 360-369) has only 7 properties — it lacks `WindowStartLocal` and `DisplayEndLocal`, which are used by the email HTML rendering logic. `WindowSummary` is a superset, not structurally identical.

7. **JSON parsing lambdas duplicated within each file and across files** — VERIFIED. The motion JSON parsing lambda (try/parse/GetProperty("motion").GetBoolean()/catch) appears at EmailRenderer.cs lines 91-93 and 100-102, and at Detail.cshtml.cs lines 320-322 and 329-331 — four functionally identical copies total. Temperature parsing also appears twice (EmailRenderer.cs lines 107-109, Detail.cshtml.cs lines 335-337).

**Additional context not mentioned in the issue:**

- `RenderDailySummaryAsync` contains significant additional logic beyond the duplicated window aggregation: battery reading queries and parsing (lines 134-164), incomplete window omission (lines 168-175), and the full HTML rendering via `BuildHtml` (lines 181-299). The method is 152 lines total, with approximately 102 lines of duplicated window logic and 50 lines of email-specific logic. This makes the extraction somewhat non-trivial since the shared portion must be cleanly separable from the email-specific battery and HTML portions.

**Priority assessment:** Labeled as "high" but should be "medium". This is a code quality enhancement for stable, working code. Issue #9 was explicitly deferred after careful review, with the assessment that the duplication is "stable and well-tested." Reopening a deliberately deferred item as "high" priority without new justification overstates the urgency. The duplication creates maintenance risk but not functional bugs.

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

The core finding -- that activity window aggregation logic is duplicated between `EmailRenderer.cs` and `Detail.cshtml.cs` -- is real and well-documented. However, several claims are slightly inaccurate or overstated:

**Claim-by-claim verification:**

1. **"Issue #9 was closed but the `ActivitySummaryBuilder` service was never created."** ACCURATE. Issue #9 exists and is closed with status "Deferred." A grep of the entire codebase confirms no `ActivitySummaryBuilder` class exists anywhere.

2. **Line references -- `EmailRenderer.cs` (lines 27-178) for `RenderDailySummaryAsync`.** ACCURATE. The method starts at line 27 and the last line of its body before the next method is line 179. However, only lines 27-129 contain the aggregation logic; lines 130-178 cover battery queries, window reversal, incomplete-window omission, and the call to `BuildHtml`. The full method is 152 lines, not "~80-100" as the description states.

3. **Line references -- `Detail.cshtml.cs` (lines 272-358) for `LoadActivitySummaryAsync`.** ACCURATE. The method begins at line 272 and ends at line 358, totaling 87 lines.

4. **"Both perform identical: time window bucketing, hub/device/reading DB queries, JSON parsing..."** ACCURATE. The time window setup (using `_emailSettings.SummaryWindowHours` and `SummaryWindowCount`, the `bucketEndLocal`/`bucketStartLocal` calculations), the hub/device/reading queries, and the per-window JSON parsing are structurally identical between both files.

5. **"`WindowSummary` class in EmailRenderer and `ActivityWindow` class in Detail are structurally identical."** OVERSTATED. `WindowSummary` (lines 311-322) has 9 properties: `Label`, `WindowStartLocal`, `DisplayEndLocal`, `DevicesWithMotion`, `TotalMotionSensors`, `TotalMotionEvents`, `TemperatureMin`, `TemperatureMedian`, `TemperatureMax`. `ActivityWindow` (lines 360-369) has only 7 properties -- it lacks `WindowStartLocal` and `DisplayEndLocal`. The classes overlap heavily but are not structurally identical; `ActivityWindow` is a subset.

6. **"JSON parsing lambdas for motion detection are duplicated within each file and across files."** ACCURATE. The identical lambda (`JsonDocument.Parse(r.Value)` -> `GetProperty("motion").GetBoolean()` with try/catch) appears twice in `EmailRenderer.cs` (lines 91-93 and 100-102) and twice in `Detail.cshtml.cs` (lines 320-322 and 329-331), for 4 total instances.

7. **"~80-100 lines" of duplicated logic.** SLIGHTLY UNDERSTATED for `EmailRenderer` (the full method is 152 lines, though pure aggregation is ~103 lines) and approximately correct for `Detail` (87 lines). The actual overlapping aggregation logic between the two files is roughly 80-90 lines, so the estimate is reasonable.

**Priority assessment:** "High" is overstated. Issue #9 itself was explicitly deferred with the rationale: "The duplication, while not ideal, is stable and well-tested." This is a code quality enhancement, not a correctness bug. The duplication carries maintenance risk but has no runtime impact. Priority should be **medium** at most.

**Additional note:** This issue is essentially a duplicate of issue #9, which was closed as "Deferred." The description should acknowledge this more clearly -- issue #9 was not closed as "resolved" but rather deferred intentionally. Reopening the same concern under a new issue number without new justification is redundant.

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

The central claim -- duplicated activity window aggregation logic between `EmailRenderer.cs` and `Detail.cshtml.cs` with no shared `ActivitySummaryBuilder` ever created -- is substantiated by the code. However, the issue overstates similarity in places and omits critical context about *why* #9 was closed without implementation.

**Detailed code-level verification:**

**1. Duplication scope and line counts.**

The issue cites "~80-100 lines" of duplicated logic at `EmailRenderer.cs` lines 27-178 and `Detail.cshtml.cs` lines 272-358. These line ranges are verified, but the characterization is imprecise. `EmailRenderer.RenderDailySummaryAsync` spans lines 27-178 (152 lines total), but the portion that parallels `LoadActivitySummaryAsync` ends at approximately line 129 (the close of the window bucketing loop). Lines 131-178 contain battery query logic (lines 135-164), an incomplete-window omission heuristic (lines 168-175), and the call to `BuildHtml` -- none of which has any counterpart in `Detail.cshtml.cs`. So the actual *duplicated* portion of `EmailRenderer` is lines 27-129 (~103 lines), and the full `LoadActivitySummaryAsync` method is lines 272-358 (~87 lines). The "~80-100 lines" estimate is a reasonable ballpark for the overlapping logic per file, but citing lines 27-178 as the duplicated range in `EmailRenderer` is misleading because it includes 49 lines of email-only logic.

**2. "WindowSummary and ActivityWindow are structurally identical" -- FALSE.**

`WindowSummary` (EmailRenderer.cs lines 311-322) has 9 properties:
- `Label`, `WindowStartLocal`, `DisplayEndLocal`, `DevicesWithMotion`, `TotalMotionSensors`, `TotalMotionEvents`, `TemperatureMin`, `TemperatureMedian`, `TemperatureMax`

`ActivityWindow` (Detail.cshtml.cs lines 360-369) has 7 properties:
- `Label`, `DevicesWithMotion`, `TotalMotionSensors`, `TotalMotionEvents`, `TemperatureMin`, `TemperatureMedian`, `TemperatureMax`

`ActivityWindow` lacks `WindowStartLocal` and `DisplayEndLocal`. These are not cosmetic extras -- `EmailRenderer` uses them for the incomplete-window omission check (line 169: comparing `DisplayEndLocal - WindowStartLocal`) and for the `FormatLabelHtml` helper (line 303). A shared model would need to be the superset (`WindowSummary`), meaning `Detail.cshtml.cs` would carry two properties it does not use. The issue's claim of structural identity is factually wrong.

**3. JSON parsing duplication -- CONFIRMED but the within-file duplication is the more impactful problem.**

The motion-parsing lambda (`JsonDocument.Parse(r.Value)` -> `GetProperty("motion").GetBoolean()` with try/catch and logging) appears at:
- `EmailRenderer.cs` lines 91-93 (for `devicesWithMotion`)
- `EmailRenderer.cs` lines 100-102 (for `totalMotionEvents`)
- `Detail.cshtml.cs` lines 320-322 (for `devicesWithMotion`)
- `Detail.cshtml.cs` lines 329-331 (for `totalMotionEvents`)

That is 4 copies. The temperature-parsing lambda appears at:
- `EmailRenderer.cs` lines 107-109
- `Detail.cshtml.cs` lines 335-337

That is 2 copies, totaling 6 duplicated JSON parsing lambdas across both files. The issue correctly identifies this. Notably, the *within-file* duplication (parsing each motion reading's JSON twice -- once to count distinct devices, once to count total events) is arguably worse than the cross-file duplication because it doubles the JSON parse work per window at runtime. Issue #9's comment about "14 JSON parses where 7 would suffice" correctly identifies this inefficiency.

**4. Minor behavioral differences the issue ignores.**

While the algorithms are structurally identical, there are small differences that would complicate a naive extraction:

- **Testability:** `EmailRenderer.RenderDailySummaryAsync` accepts an optional `nowUtc` parameter and a `CancellationToken`; `LoadActivitySummaryAsync` uses `DateTime.UtcNow` directly and passes no cancellation tokens to its DB queries.
- **Logging on empty readings:** `EmailRenderer` logs when no readings are found (lines 64-69); `Detail.cshtml.cs` does not.
- **Motion sensor count query ordering:** In `EmailRenderer`, the motion sensor count query (lines 72-74) happens *after* the readings query and logging; in `Detail.cshtml.cs` (lines 297-299) it happens *before* the readings query. The result is the same but the placement differs.

These are minor but they mean an `ActivitySummaryBuilder` extraction is not a simple copy-paste -- it requires design decisions about the API surface.

**5. Priority assessment.**

"High" priority is not justified. Issue #9 was deliberately deferred with the explicit rationale: "The duplication, while not ideal, is stable and well-tested. Deferring until there's a stronger driver for the refactor." This issue (#75) provides no new justification for elevated urgency -- no bugs caused by the duplication, no new consumers of the duplicated logic, no drift between the two implementations that has caused problems. The duplication is a maintenance concern warranting **medium** priority at most.

**6. Relationship to issue #9.**

This issue is functionally a reopening of issue #9. The description says "#9 was closed" but omits the critical detail that #9 was closed as *deliberately deferred*, not as completed or resolved. This framing makes it sound like the work was supposed to have been done and was missed, when in fact a conscious decision was made to defer it. A more accurate framing would be: "Issue #9 was deferred; this reopens the discussion because [new reason]." Without a new driver, this issue is redundant with #9 and should either reference #9 for reopening or provide a concrete justification for why the deferral decision should be revisited.

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

The core claim -- that duplicated activity window aggregation logic exists between `EmailRenderer.cs` and `Detail.cshtml.cs`, and that `ActivitySummaryBuilder` was never extracted despite issue #9 proposing it -- is accurate and well-supported by the code. However, the issue contains factual errors and overstatements that undermine its precision.

**Verified claims:**

1. **Duplication exists and is substantial.** Side-by-side comparison of `EmailRenderer.RenderDailySummaryAsync` (lines 27-129) and `DetailModel.LoadActivitySummaryAsync` (lines 272-357) confirms the following are functionally identical: timezone conversion logic, window configuration from `_emailSettings.SummaryWindowHours`/`SummaryWindowCount`, bucket boundary arithmetic (`bucketEndLocal`/`bucketStartLocal`), three sequential DB queries (hubs by customer+status, devices by hub, readings by device+time+type), the per-window iteration loop including UTC conversion of local boundaries, motion/temperature filtering, JSON parsing, and summary object population with the same field calculations (including the `temperatures[temperatures.Count / 2]` median approximation and `temperatures.First()`/`Last()` for min/max). Both methods end with `Reverse()`. This is genuine, non-trivial duplication.

2. **No `ActivitySummaryBuilder` exists.** A codebase-wide grep confirms zero matches. Issue #9 was closed with "Deferred" status.

3. **JSON parsing lambda duplication is real.** The motion-parsing lambda (`JsonDocument.Parse` -> `GetProperty("motion").GetBoolean()` with identical try/catch/LogWarning) appears 4 times total: twice in `EmailRenderer.cs` (lines 91-93 for `devicesWithMotion`, lines 100-102 for `totalMotionEvents`) and twice in `Detail.cshtml.cs` (lines 320-322, lines 329-331). The temperature-parsing lambda appears twice (EmailRenderer line 108, Detail line 336). This is 6 nearly-identical JSON parsing blocks across the codebase.

**Inaccurate or overstated claims:**

1. **"WindowSummary and ActivityWindow are structurally identical" -- FALSE.** `WindowSummary` (EmailRenderer.cs, lines 311-322) has 9 properties including `WindowStartLocal` and `DisplayEndLocal`. `ActivityWindow` (Detail.cshtml.cs, lines 360-369) has only 7 properties -- it lacks those two. These extra properties are used by EmailRenderer for incomplete-window omission (line 169: comparing `DisplayEndLocal - WindowStartLocal`) and for the `FormatLabelHtml` helper (line 303). `ActivityWindow` is a strict subset of `WindowSummary`, not structurally identical. An extracted shared model would need the superset, and `Detail.cshtml.cs` would need to start populating the extra fields or a different approach would be needed.

2. **"~80-100 lines" line count is ambiguous.** The issue claims the range in EmailRenderer is lines 27-178 (152 lines), but then says "~80-100 lines" of duplicated logic. The actually shared aggregation logic in EmailRenderer is lines 27-129 (~103 lines). Lines 130-178 contain EmailRenderer-specific logic: battery reading queries and parsing (lines 134-164), incomplete-window omission (lines 168-175), and the `BuildHtml` call (line 178) -- none of which appears in `Detail.cshtml.cs`. The `Detail.cshtml.cs` method is 86 lines. So the duplicated core is roughly 85-103 lines depending on which file you measure, making "~80-100" a reasonable but imprecise estimate.

3. **Minor method signature differences are not acknowledged.** `RenderDailySummaryAsync` accepts an optional `nowUtc` parameter and a `CancellationToken`, enabling testability and cancellation. `LoadActivitySummaryAsync` uses `DateTime.UtcNow` directly and has no cancellation support. An extraction would need to accommodate the more flexible signature.

**Critical assessment of priority and framing:**

The "high" priority is not justified. Issue #9 was deliberately deferred -- its second comment explicitly states: "The duplication, while not ideal, is stable and well-tested. Deferring until there's a stronger driver for the refactor." This issue (#75) reopens the same concern without citing any new driver -- no bugs caused by the duplication, no new consumer of the logic, no drift between the two implementations. The duplication is a maintenance risk, not an active problem. **Medium** priority would be appropriate.

Furthermore, this issue is effectively a duplicate of #9. Issue #9 was not "closed as resolved" -- it was deferred. Creating a new issue number for the same concern obscures the history and loses the valuable context in #9's comments (which describe the consolidation plan from #11 and #56 and the explicit deferral rationale). The appropriate action would have been to reopen #9 with a comment explaining why the deferral should be revisited.

### claude — 2026-03-02

Comprehensive review (code quality) found additional detail:

The code quality review independently rated this as **high severity** (not low). The duplication between `EmailRenderer.RenderDailySummaryAsync` (lines 27-178) and `DetailModel.LoadActivitySummaryAsync` (lines 272-358) remains extensive — both methods compute identical bucket windows, query the same hub/device/reading chains, use identical JSON parsing lambdas for motion and temperature, and compute the same aggregates (devicesWithMotion, totalMotionEvents, temperature min/median/max). The JSON parsing lambdas alone appear 4+ times across both files. This is the single highest-impact code quality issue in the codebase.

### claude — 2026-03-02

Comprehensive review (code quality) confirms this duplication persists with specific details:

The `LoadActivitySummaryAsync` method in `Detail.cshtml.cs` (lines 272-358) remains a near-verbatim copy of the core windowing logic from `EmailRenderer.RenderDailySummaryAsync`. Both independently: (1) compute window boundaries from `SummaryWindowHours`/`SummaryWindowCount`, (2) query hub IDs, device IDs, and readings, (3) parse motion/temperature JSON with identical inline lambdas, (4) compute the same metrics, (5) reverse the windows list. The JSON parsing lambdas (EmailRenderer lines 91-103, Detail lines 319-342) are character-for-character identical.

Additionally, `EmailRenderer.WindowSummary` and `DetailModel.ActivityWindow` are structurally identical private DTOs with the same properties (`Label`, `DevicesWithMotion`, `TotalMotionSensors`, `TotalMotionEvents`, `TemperatureMin`, `TemperatureMedian`, `TemperatureMax`).
