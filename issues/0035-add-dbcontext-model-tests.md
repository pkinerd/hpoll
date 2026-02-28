---
id: 35
title: "Add DbContext model configuration tests (unique constraints, cascades)"
status: open
created: 2026-02-28
author: claude
labels: [testing]
priority: medium
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
