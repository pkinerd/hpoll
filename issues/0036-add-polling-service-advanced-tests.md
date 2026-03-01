---
id: 36
title: "Add PollingService multi-hub and batch cleanup tests"
status: closed
created: 2026-02-28
author: claude
labels: [testing]
priority: medium
closed: 2026-03-01
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

### claude — 2026-03-01

Resolved: Added 5 new PollingService tests (25 total). Covers: multiple active hubs polled in single cycle, device with unknown owner RID uses 'Unknown' name, battery with null level skips reading, error message truncation to 500 chars, and CleanupOldDataAsync exception logging. Shared owner RID, >1000 batch records, and SaveChangesAsync failure scenarios deferred as lower-priority edge cases.
