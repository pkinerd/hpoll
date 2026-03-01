---
id: 70
title: "DeviceReading inline comments outdated — missing battery reading type"
status: open
created: 2026-03-01
author: claude
labels: [documentation]
priority: medium
---

## Description

The inline comments on `DeviceReading` entity properties are outdated:

- Line 9: `ReadingType` comment says `// "motion" or "temperature"` — but `"battery"` is also a valid reading type (written by `PollingService.cs` line 200)
- Line 10: `Value` comment describes JSON format for motion and temperature but omits the battery format `{"battery_level": 85, "battery_state": "normal"}`

**Location:** `src/Hpoll.Data/Entities/DeviceReading.cs` lines 9-10

**Recommendation:** Update line 9 to `// "motion", "temperature", or "battery"` and line 10 to include the battery JSON schema in the format description.

*Found during comprehensive review (documentation review).*

## Comments
