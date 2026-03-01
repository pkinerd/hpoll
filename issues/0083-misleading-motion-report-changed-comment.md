---
id: 83
title: "Misleading HueMotionReport.Changed XML doc comment"
status: open
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
