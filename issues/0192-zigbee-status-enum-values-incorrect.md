---
id: 192
title: "HueZigbeeConnectivityResource.Status enum values incorrect per API spec"
status: open
created: 2026-03-30
author: claude
labels: [bug, documentation]
priority: medium
---

## Description

The `<summary>` on `HueZigbeeConnectivityResource.Status` in
`src/Hpoll.Core/Models/HueApiModels.cs` (line 219) lists five enum values:
`connected`, `disconnected`, `connectivity_issue`, `unidirectional_incoming`,
and `configuration_error`.

However, the Hue CLIP v2 OpenAPI spec defines the values as: `connected`,
`disconnected`, `connectivity_issue`, `unidirectional_incoming`, and
**`pending_discovery`** — not `configuration_error`.

**Impact:**
- `configuration_error` may never be emitted by the API, meaning the display
  label for it in `EmailRenderer.BuildHtml` and `Detail.cshtml` is dead code.
- `pending_discovery` is a real API value that is not documented or handled,
  meaning a device in discovery state would fall through as "unknown" in the
  switch expression.

**Recommendation:**
1. Replace `configuration_error` with `pending_discovery` in the `<summary>`.
2. Add `pending_discovery` handling to the switch expressions in
   `EmailRenderer.BuildHtml` and `Detail.cshtml`.
3. Verify whether `configuration_error` was ever observed in practice before
   removing its handling entirely.

*Found during comprehensive review (documentation review + Hue API docs).*

## Comments
