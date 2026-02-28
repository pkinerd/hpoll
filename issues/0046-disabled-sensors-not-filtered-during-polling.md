---
id: 46
title: "Disabled sensors are not filtered during polling"
status: open
created: 2026-02-28
author: claude
labels: [bug]
priority: medium
---

## Description

**Severity: Medium**

In `PollingService.cs` lines 132-156 (motion) and 159-181 (temperature), the code processes all sensor resources from the Hue API without checking the `Enabled` property. The `HueMotionResource` and `HueTemperatureResource` models both include an `Enabled` field (`HueApiModels.cs` lines 41, 74).

Per the Hue CLIP API documentation: `enabled` indicates "true when sensor is activated, false when deactivated." A disabled sensor should not generate readings, but if the API still returns data for disabled sensors, the current code would record it.

**Impact:** Potentially recording stale or irrelevant data from disabled sensors, leading to inaccurate activity summaries and email reports.

**Remediation:** Add `if (!motion.Enabled) continue;` and `if (!temp.Enabled) continue;` checks before processing sensor data.

## Comments
