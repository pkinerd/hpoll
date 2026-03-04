---
id: 146
title: "GetOrCreateDeviceAsync edge case (null/empty HueDeviceId) not tested"
status: open
created: 2026-03-04
author: claude
labels: [testing]
priority: low
---

## Description

The `PollingService.GetOrCreateDeviceAsync` method (line 333-356, `private`) handles device upsert during polling.

**Existing coverage (via integration-style tests through `PollAllHubsAsync`):**
- `PollHub_CreatesNewDeviceIfNotExists` — verifies new device creation
- `PollHub_UpdatesDeviceName_WhenChanged` — verifies name update on existing device
- `PollHub_DeviceNameChanged_UpdatedAtUsesTimeProvider` — verifies `UpdatedAt` on name change
- `PollHub_NewDevice_DoesNotSetUpdatedAtFromTimeProvider` — verifies creation path

**Remaining gap:** Behavior with empty or null `HueDeviceId` is not tested. This is an edge case unlikely to occur from valid Hue API data (the `Owner.Rid` field is always a non-empty UUID).

**Note:** The method is `private` (not `internal`), so direct unit testing would require changing the access modifier. The current integration-style tests through `PollAllHubsAsync` effectively cover the main code paths.

**Found by:** Comprehensive review — unit testing review.

## Comments

### critical-review — 2026-03-04

Critical review: ADJUST. Retitled and lowered priority from medium to low. Four existing tests via PollAllHubsAsync already cover all main branches (create new, return existing, update name). Method is private (not internal). Only remaining gap is null/empty HueDeviceId edge case.
