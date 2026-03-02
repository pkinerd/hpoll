---
id: 123
title: "Extract 60-minute incomplete-window threshold into named constant"
status: closed
closed: 2026-03-02
created: 2026-03-02
author: claude
labels: [documentation]
priority: low
---

## Description

The incomplete-window trimming logic in `EmailRenderer.RenderDailySummaryAsync` (`src/Hpoll.Email/EmailRenderer.cs`, lines 168-175) removes the newest window if it has less than 60 minutes of data. The `60` literal is a hardcoded magic number. While there is already a comment on line 168 explaining the intent ("Omit the newest window if it has less than 60 minutes of data (likely incomplete)"), the rationale for the specific 60-minute threshold is not documented, and the value is not a named constant.

Note: The window snapping formula (line 42) already has a comment ("Snap to the end of the current window so it's always included"), and the extra-bucket overlap concept is documented both in the `IEmailRenderer` interface XML doc comment and at line 37 ("Query window covers the full span plus one extra bucket for overlap"). These aspects are adequately documented.

**Category:** missing-docs / code-quality
**Severity:** low
**Found by:** Documentation review (comprehensive review 2026-03-02)

### Recommendation

Extract the `60` literal on line 169 into a named constant (e.g., `MinimumWindowDisplayMinutes = 60`) with a comment explaining the chosen threshold value. Note: the threshold is not architecturally coupled to `PollingSettings.IntervalMinutes` — `EmailRenderer` only depends on `EmailSettings`.

## Comments

### claude — 2026-03-02

Fixed: Extracted the magic number 60 into a named constant `MinimumWindowDisplayMinutes` in EmailRenderer with an XML doc comment explaining its purpose. Updated the usage site to reference the constant.
