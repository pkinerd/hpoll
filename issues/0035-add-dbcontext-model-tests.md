---
id: 35
title: "Add DbContext model configuration tests (unique constraints, cascades)"
status: closed
created: 2026-02-28
author: claude
labels: [testing]
priority: medium
closed: 2026-03-01
---

## Description

The `HpollDbContext` model configuration is indirectly exercised but never explicitly tested:

- Unique index on `Customer.Email` — never verified
- Unique composite index on `Hub.HueBridgeId` — never verified
- Unique composite index on `(Device.HubId, Device.HueDeviceId)` — never verified
- Cascade delete `Customer -> Hubs -> Devices -> Readings` — never verified

**Note:** These tests require SQLite in-memory (not EF InMemoryDatabase) since InMemory doesn't enforce unique constraints.

Also, `ConfigSeederTests` and `SesEmailSenderTests` are in `Hpoll.Core.Tests` but test `Hpoll.Data` and `Hpoll.Email` classes respectively — consider reorganizing test projects.

## Comments

### claude — 2026-03-01

Resolved: Created `DbContextModelTests.cs` in Hpoll.Admin.Tests with 5 tests using SQLite in-memory (not EF InMemoryDatabase, as recommended). Tests: Customer duplicate email unique constraint, Hub duplicate BridgeId unique constraint, Device duplicate (HubId, HueDeviceId) composite unique constraint, cascade delete Customer->Hubs->Devices->Readings, cascade delete Hub->Devices->Readings. All constraints verified.
