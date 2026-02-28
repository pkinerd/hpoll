---
id: 41
title: "Add inline comments for complex algorithms and design decisions"
status: open
created: 2026-02-28
author: claude
labels: [documentation]
priority: low
---

## Description

Several complex code sections lack explanatory comments:

1. **Email rendering bucket-snapping formula** (`EmailRenderer.cs` line 41): `nowLocal.Hour / windowHours * windowHours + windowHours` uses integer-division truncation with no explanation.

2. **Motion event color thresholds** (`EmailRenderer.cs` lines 186-189): Why 0=red, 1=yellow, 2+=green? Why cap at 5 events?

3. **Token refresh exponential backoff** (`TokenRefreshService.cs` line 124): `Math.Pow(2, retry + 1)` — no comment explaining the 2s, 4s, 8s strategy.

4. **WAL mode pragma** (`Worker/Program.cs` line 61, `Admin/Program.cs` line 79): No explanation of why WAL is needed (concurrent reads from admin while worker writes).

5. **Zero TODO/FIXME markers** in the entire codebase — known limitations and technical debt are untracked.

6. **`docs/` directory** contains only placeholder READMEs with no actual documentation content.

## Comments
