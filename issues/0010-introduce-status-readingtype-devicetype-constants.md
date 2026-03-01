---
id: 10
title: "Introduce string constants or enums for Status, ReadingType, and DeviceType fields"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: high
---

## Description

Raw string literals are used throughout the codebase for `Customer.Status`, `Hub.Status`, `DeviceReading.ReadingType`, and `Device.DeviceType` fields with no type safety.

**Status values** (`"active"`, `"inactive"`, `"needs_reauth"`) appear across ~14 files including:
- Entity defaults, Admin page handlers, Worker services, Email renderer

**ReadingType values** (`"motion"`, `"temperature"`, `"battery"`) appear across 4 files. The inline comment on `DeviceReading.ReadingType` says `"motion" or "temperature"` but the code also uses `"battery"` — making the comment stale.

**DeviceType values** (`"motion_sensor"`, `"temperature_sensor"`, `"battery"`) appear in PollingService, EmailRenderer, and Detail page.

**Recommendation:** Create `CustomerStatus`, `HubStatus`, `ReadingType`, and `DeviceType` as either C# enums (with EF Core value conversion) or static string constant classes in `Hpoll.Core`. Replace all raw string literals.

## Comments

### claude — 2026-03-01

**Consolidated from #0070 (closed as subset of this issue).**

When introducing the `ReadingType` enum/constants, also update `DeviceReading.cs` line 10's `Value` comment to include the battery JSON schema: `{"battery_level": 85, "battery_state": "normal"}` (currently only documents motion and temperature formats). The stale `ReadingType` comment on line 9 will be naturally replaced by the enum's own documentation.
