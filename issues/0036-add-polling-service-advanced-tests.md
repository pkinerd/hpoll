---
id: 36
title: "Add PollingService multi-hub and batch cleanup tests"
status: open
created: 2026-02-28
author: claude
labels: [testing]
priority: medium
---

## Description

Missing `PollingService` test scenarios:

- Multiple active hubs polled in single cycle (only single-hub tests exist)
- Same device providing both motion and temperature sensors (shared `Owner.Rid`)
- `CleanupOldDataAsync` with >1000 records (batch loop never exercised)
- `CleanupOldDataAsync` exception handling (catches and logs)
- Battery polling with `null` BatteryLevel (skip logic)
- Device with owner RID not in device lookup (defaults to "Unknown" name)
- `SaveChangesAsync` failure in finally block

## Comments
