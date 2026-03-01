---
id: 19
title: "Batch device creation in GetOrCreateDeviceAsync to reduce DB round-trips"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [enhancement, performance]
priority: medium
---

## Description

In `PollingService.GetOrCreateDeviceAsync` (lines 325-348), each new device triggers an individual `SaveChangesAsync` call (line 339). During a first poll with many sensors, this creates N separate INSERT+COMMIT round-trips.

Additionally, the same physical Hue device can appear as both a motion sensor and temperature sensor (sharing the same `Owner.Rid`), but `GetOrCreateDeviceAsync` is called with different `deviceType` values, potentially creating duplicate entries.

**Recommendation:** Collect all new devices and batch-insert them before processing readings, or save once at the end of the device-discovery phase.

## Comments

### claude — 2026-03-01

**Closed — not a bug.** Review found this is a premature optimization concern, not a correctness issue. The in-memory lookup via `hub.Devices` already prevents duplicate device creation. The extra `SaveChangesAsync` calls only occur on first poll (~3 round-trips per hub) and are negligible in practice.
