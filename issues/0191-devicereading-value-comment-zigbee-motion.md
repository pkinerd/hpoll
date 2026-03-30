---
id: 191
title: "DeviceReading.Value comment missing zigbee_connectivity and misleading motion semantics"
status: open
created: 2026-03-30
author: claude
labels: [documentation]
priority: low
---

## Description

The inline comment on `DeviceReading.Value` (line 9 of
`src/Hpoll.Data/Entities/DeviceReading.cs`) is incomplete and misleading:

1. **Missing 4th JSON shape**: The comment lists motion, temperature, and battery
   formats but omits the `zigbee_connectivity` format:
   `{"status": "connected", "mac_address": "..."}`.

2. **Misleading motion value**: The comment shows `"motion": true` as the stored
   value, but the actual logic in `PollingService` stores `motionDetected` which
   is derived from `Changed > motionCutoff` — the value can be `false`. The
   comment implies motion readings always have `motion: true`.

**Note:** #0070 addressed the missing battery type but was closed before the
zigbee format or motion semantics issues were identified.

**Recommendation:** Update the comment to list all four JSON shapes and clarify
that the `motion` boolean reflects whether motion was detected during the
polling interval, not the raw API boolean.

*Found during comprehensive review (documentation review).*

## Comments
