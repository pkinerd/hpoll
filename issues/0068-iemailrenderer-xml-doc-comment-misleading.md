---
id: 68
title: "IEmailRenderer XML doc comment is misleading — hardcodes configurable values"
status: open
created: 2026-03-01
author: claude
labels: [documentation]
priority: medium
---

## Description

The only XML doc comment in the entire `src/` directory (on `IEmailRenderer.RenderDailySummaryAsync`) is inaccurate:

- States the email covers "the last 28 hours of data" — but the window count and size are configurable via `EmailSettings.SummaryWindowHours` (default 4) and `EmailSettings.SummaryWindowCount` (default 7)
- States "standardised 4-hour windows aligned to midnight" — but the actual bucketing snaps to the current hour aligned to `windowHours`, not necessarily midnight
- Lists "00:00, 04:00, 08:00, 12:00, 16:00, 20:00" — only 6 windows, but default config produces 7

**Location:** `src/Hpoll.Core/Interfaces/IEmailRenderer.cs` lines 5-9

**Recommendation:** Update the comment to reference the configurable settings rather than hardcoded values: "Renders the daily summary email. By default covers 28 hours of data (configurable via SummaryWindowHours and SummaryWindowCount). Readings are bucketed into time windows aligned to multiples of SummaryWindowHours in the customer's timezone."

*Found during comprehensive review (documentation review, cross-referenced with Hue API docs).*

## Comments
