---
id: 51
title: "BatteryAlertThreshold boundary condition — device at exactly threshold level is neither shown nor alerted"
status: open
created: 2026-02-28
author: claude
labels: [bug]
priority: medium
---

## Description

**Severity: Medium**

In `EmailRenderer.cs` line 249, the battery section visibility check uses strict less-than:
```csharp
if (batteryStatuses.Any(b => b.BatteryLevel < batteryAlertThreshold))
```

And the color coding at line 255 also uses strict less-than:
```csharp
var color = b.BatteryLevel < batteryLevelCritical ? "#e74c3c" : ...
```

With the default configuration where `BatteryAlertThreshold = 30` and `BatteryLevelCritical = 30`, a device at exactly 30% battery:
- Would NOT trigger the battery section to appear (30 is not < 30)
- Would show as green if the section appeared for another reason (30 is not < 30)

This boundary condition likely does not match the intended behavior — a device at exactly the "critical" threshold should probably be shown and colored as critical.

**Remediation:** Change `<` to `<=` for the alert threshold check: `b.BatteryLevel <= batteryAlertThreshold`. Also review whether `BatteryAlertThreshold` and `BatteryLevelCritical` should be consolidated into a single setting since they serve overlapping purposes with identical defaults.

## Comments
