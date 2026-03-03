---
id: 133
title: "Device.RoomName property is never populated during polling"
status: open
created: 2026-03-03
author: claude
labels: [code-quality]
priority: low
---

## Description

The `RoomName` property on the `Device` entity (`src/Hpoll.Data/Entities/Device.cs` line 11)
is included in the database schema but is never populated during polling.

`PollingService.GetOrCreateDeviceAsync` (line 339) creates devices with only `HueDeviceId`,
`DeviceType`, and `Name` — `RoomName` defaults to `string.Empty`.

The admin portal **does** display this field in the Hub Detail page's device table as a "Room"
column (`src/Hpoll.Admin/Pages/Hubs/Detail.cshtml` line 113), but because `RoomName` is never
populated, this column is always blank. An integration test in `HubsPageTests.cs` (line 336)
explicitly populates `RoomName` and asserts it renders correctly, confirming the display layer
is wired up and tested.

This is a partially implemented feature — the display layer is in place and tested, but the
data population during polling was never completed. Populating this field would require calling
the Hue API `/resource/room` endpoint and cross-referencing devices to their assigned rooms.

**Recommendation:** Either:
1. Populate `RoomName` by calling the Hue rooms API during polling and mapping devices to rooms
2. Add a `<summary>` doc comment noting it is reserved for future room-based grouping
3. Remove the property and the blank "Room" column if room support is not planned

**Found by:** Comprehensive review — documentation review.

## Comments
