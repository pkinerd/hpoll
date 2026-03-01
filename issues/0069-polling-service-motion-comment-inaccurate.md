---
id: 69
title: "PollingService motion detection comment is inaccurate per Hue API docs"
status: open
created: 2026-03-01
author: claude
labels: [documentation]
priority: medium
---

## Description

The inline comment at `PollingService.cs` lines 121-125 states: "The Hue API motion boolean is momentary and resets quickly, so with 60-minute polling we'd miss most events if we relied on it directly."

Per the Hue CLIP v2 API documentation (`sensors.md`):
- `motion_report.motion` reflects the **current sensor state** (not a latched event)
- `motion_report.changed` is the **timestamp of the last state transition**
- The motion sensor holds its `true` state for a sensor-specific cooldown period (typically 10-30 seconds), not "momentary"

The comment's claim that the boolean "resets quickly" oversimplifies. The code's approach (using `changed` timestamp as a "motion occurred since last poll" indicator) is correct, but the explanation of *why* is imprecise.

**Location:** `src/Hpoll.Worker/Services/PollingService.cs` lines 121-125

**Recommendation:** Revise to: "The Hue API motion_report.motion reflects the current sensor state and motion_report.changed is the timestamp of the last state transition. Since polling happens infrequently (e.g., every 60 minutes), the motion boolean will likely have returned to false by the time we poll. Instead, we check whether changed is more recent than our last poll — if so, the sensor detected motion at some point during the interval."

*Found during comprehensive review (documentation review, cross-referenced with Hue API sensors.md).*

## Comments
