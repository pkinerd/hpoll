---
id: 189
title: "Stale motion and temperature devices not cleaned up when removed from hub"
status: closed
created: 2026-03-22
author: claude
labels: [bug]
priority: medium
closed: 2026-03-22
---

## Description

The stale device cleanup added for battery devices (commit 5fec691) does not extend to motion or temperature sensors. When a motion or temperature sensor is physically removed from a Hue hub, its `Device` record and associated `DeviceReading` records remain in the database indefinitely.

### Impact

- **Email reports**: Stale motion sensors inflate the `motionSensorCount` in `EmailRenderer.cs` (lines 75-77), misrepresenting the number of monitored sensors. Stale temperature sensors could appear with outdated readings.
- **Database bloat**: Orphaned `Device` rows accumulate over time for removed sensors.
- **Data retention partially masks the issue**: Old readings are purged after `DataRetentionHours` (168h default), but `Device` records persist indefinitely.

### Affected Code

- `src/Hpoll.Worker/Services/PollingService.cs` — motion polling (line ~156) and temperature polling (line ~183) lack the stale-device removal logic present in battery polling (lines 237-254).

### Suggested Fix

Apply the same pattern used for battery devices: track `currentMotionDeviceIds` and `currentTemperatureDeviceIds` in HashSets during polling, then after processing, query for and remove any `Device` records (and their readings) of those types that are no longer in the API response.

## Comments
