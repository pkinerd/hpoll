---
id: 116
title: "Add test for PollingService motion cutoff after extended downtime"
status: closed
closed: 2026-03-02
created: 2026-03-02
author: claude
labels: [testing]
priority: medium
---

## Description

The motion cutoff calculation in `PollingService.PollHubAsync` uses `Math.Min(hub.LastPolledAt.Value.Ticks, intervalCutoff.Ticks)` to handle recovery after extended service downtime, but only the normal-interval branch of this `Math.Min` is tested.

**Location:** `src/Hpoll.Worker/Services/PollingService.cs` lines 148-151

**Logic:**
```csharp
var intervalCutoff = pollTime.AddMinutes(-_settings.IntervalMinutes);
var motionCutoff = hub.LastPolledAt.HasValue
    ? new DateTime(Math.Min(hub.LastPolledAt.Value.Ticks, intervalCutoff.Ticks), DateTimeKind.Utc)
    : intervalCutoff;
```

**How Math.Min works here:** "6 hours ago" has *smaller* tick values than "60 minutes ago" (further in the past = fewer ticks from epoch). So `Math.Min` correctly selects the older `LastPolledAt` timestamp when the service has been down, extending the detection window back to the last poll.

**Existing test coverage:**
- `PollHub_DetectsMotion_WhenChangedIsAfterCutoff`: Uses no `LastPolledAt` — takes the `HasValue == false` branch, never enters `Math.Min`
- `PollHub_NoMotion_WhenChangedIsBeforeCutoff`: Sets `LastPolledAt` = 30 min ago (more recent than `intervalCutoff`), so `intervalCutoff` wins the `Math.Min` — covers normal operation only

**Gap:** Neither test exercises the branch where `LastPolledAt` is older than `intervalCutoff` (extended downtime recovery).

**Recommendation:**
Add two tests:
1. **Motion detected after downtime:** `LastPolledAt` = 6 hours ago, motion `Changed` = 3 hours ago → motion IS detected (because `Changed > LastPolledAt` even though `Changed < intervalCutoff`)
2. **No motion before last poll:** `LastPolledAt` = 6 hours ago, motion `Changed` = 7 hours ago → motion NOT detected (because `Changed < LastPolledAt`)

Both tests validate the downtime-recovery branch of the `Math.Min` logic.

## Comments

### claude — 2026-03-02

Implemented: added 2 downtime recovery tests for Math.Min branch in motion cutoff logic

