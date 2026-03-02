---
id: 83
title: "Misleading HueMotionReport.Changed XML doc comment"
status: closed
closed: 2026-03-02
created: 2026-03-01
author: claude
labels: [documentation]
priority: medium
---

## Description

The XML doc comment on `HueMotionReport` in `HueApiModels.cs` (lines 66-69) states:

> "Changed is the last time the motion property value changed (not when motion was first
> detected). The sensor returns to false after its configured timeout period."

This is misleading:
1. The parenthetical "(not when motion was first detected)" is incorrect. When motion
   transitions from `false` to `true`, the `Changed` timestamp IS when motion was first
   detected. The `Changed` timestamp updates on transitions in both directions.
2. The claim about a "configured timeout period" is an implementation assumption not stated
   in the Hue API documentation.

**Found by:** Comprehensive review — documentation review, cross-referenced with Hue CLIP v2
API docs.

**Recommendation:** Revise to: "Changed is the last time the motion property value changed
(transitions both to true and back to false). The sensor may return to false after a
device-specific cooldown period."

## Comments

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

**Claim 1: Line numbers and quoted text.**
The issue states the XML doc comment is at lines 66-69. The actual comment spans lines 66-71 (the `<summary>` block for `HueMotionReport`). The quoted text accurately reflects lines 68-70 of the file: `Changed is the last time the motion property value changed (not when motion was first detected). The sensor returns to false after its configured timeout period.` This claim is accurate.

**Claim 2: The parenthetical "(not when motion was first detected)" is incorrect.**
The issue argues that when motion transitions from `false` to `true`, the `Changed` timestamp IS when motion was first detected, and therefore the parenthetical is wrong. This reasoning is sound in a narrow reading: if `Changed` updates on every transition (both false-to-true and true-to-false), then the parenthetical is misleading because on a false-to-true transition, the `Changed` timestamp does in fact represent the moment motion was first detected.

However, the existing comment's intent is arguably to clarify a subtle point: `Changed` does not only update when motion starts -- it also updates when motion stops. The phrase "(not when motion was first detected)" is trying to warn developers that `Changed` might reflect a transition back to `false`, not necessarily the start of motion. The wording is clumsy but the intent is defensible. The issue's criticism is technically correct but slightly overstates the problem by calling it "incorrect" rather than "imprecise" or "misleadingly worded."

**Claim 3: "configured timeout period" is an implementation assumption.**
The issue claims that the phrase "configured timeout period" is not stated in the Hue API documentation. The Hue CLIP v2 API documentation does describe that motion sensors return to `false` after a period, but the exact terminology varies. The Hue motion sensor hardware does have a sensitivity setting and a built-in cooldown/timeout, but the official API docs for the `motion` resource do not explicitly expose a user-configurable "timeout period" setting in the CLIP v2 API. Community sources describe this as a "device-specific cooldown" rather than a "configured timeout." This claim is partially valid -- the comment's use of "configured" implies a user-settable parameter that may not be directly exposed in the v2 API, though the underlying behavior (sensor returning to false after some period) is real.

**Claim 4: Cross-referencing with Hue CLIP v2 API docs.**
The issue claims it was "cross-referenced with Hue CLIP v2 API docs." The encrypted docs archive in the repo could not be extracted for independent verification. Based on publicly available sources and community documentation, the `changed` field in `motion_report` represents the timestamp of the last state change of the `motion` property, which aligns with the issue's general understanding.

**Assessment of how `Changed` is actually used in the codebase.**
In `PollingService.cs` (line 165), the code does: `var motionDetected = motion.Motion.MotionReport.Changed > motionCutoff;` This uses `Changed` as a timestamp to determine if any state change occurred since the last poll. The accompanying comment (lines 143-147) correctly notes that the Hue motion boolean "resets quickly" and that polling at long intervals would miss events. The existing XML doc comment, while imprecise, does not appear to have caused any actual bug in usage -- the polling logic correctly treats `Changed` as a "last transition" timestamp regardless of direction.

**Assessment of severity/priority.**
The issue is labeled as "medium" priority with a "documentation" label. Given that this is an XML doc comment clarification with no functional impact, "low" priority would be more appropriate. The existing comment is imprecise rather than dangerously wrong, and the actual code usage is correct.

**Corrections needed to the issue description.**
- The issue says lines 66-69 but the summary block runs lines 66-71. Minor inaccuracy.
- The issue overstates the problem by calling the parenthetical "incorrect" when it is more accurately "imprecise" or "misleadingly worded." The comment is trying to convey a real nuance (that `Changed` may not reflect the most recent motion-start event) but does so poorly.
- The recommended replacement text ("transitions both to true and back to false") is a reasonable improvement but could itself be more precise by noting the practical implication for polling-based consumers.
- Priority should be "low" rather than "medium" for a documentation-only issue with no functional impact.

### claude — 2026-03-02

Fixed: Revised HueMotionReport.Changed XML doc comment — replaced misleading parenthetical with 'transitions both to true and back to false' and changed 'configured timeout period' to 'device-specific cooldown period'.
