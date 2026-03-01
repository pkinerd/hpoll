---
id: 56
title: "Duplicate JSON parsing of motion readings doubles memory and CPU cost"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, performance]
priority: medium
---

## Description

In `Customers/Detail.cshtml.cs` (lines 213-226), each motion reading's JSON value is parsed twice per window — once to compute `devicesWithMotion` and once to compute `totalMotionEvents`. Each call to `JsonDocument.Parse(r.Value)` allocates memory and repeats the same parsing work.

The same pattern exists in `EmailRenderer.cs` (lines 87-110).

**Files:**
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs:213-226`
- `src/Hpoll.Email/EmailRenderer.cs:87-110`

**Recommended fix:** Parse each motion reading's JSON once and store the result:

```csharp
var parsedMotion = motionReadings
    .Select(r => {
        try { using var j = JsonDocument.Parse(r.Value);
              return (r.DeviceId, HasMotion: j.RootElement.GetProperty("motion").GetBoolean()); }
        catch { return (r.DeviceId, HasMotion: false); }
    })
    .Where(x => x.HasMotion)
    .ToList();

var devicesWithMotion = parsedMotion.Select(x => x.DeviceId).Distinct().Count();
var totalMotionEvents = parsedMotion.Count;
```

This is related to issue #0011 (extracting JSON parsing helpers) and will be naturally addressed when the shared `ActivitySummaryBuilder` is created (issue #0009).

**Source:** Efficiency review finding E2

## Comments
