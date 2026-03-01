---
id: 76
title: "Inconsistent TimeProvider vs DateTime.UtcNow usage across codebase"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, code-quality]
priority: high
---

## Description

Issue #67 addressed entity default values using `DateTime.UtcNow` and was closed, but the broader
inconsistency remains. Worker background services inject `TimeProvider` for testability, but
many code paths still use `DateTime.UtcNow` directly:

- `PollingService.GetOrCreateDeviceAsync` (line 353)
- `SystemInfoService` (lines 31, 38, 59, 69)
- `EmailRenderer` (line 30)
- All Admin page models: `Detail.cshtml.cs` (lines 101, 119, 157, 200, 215, 275),
  `Create.cshtml.cs` (line 75), `Hubs/Detail.cshtml.cs` (lines 61, 68, 84, 118),
  `Index.cshtml.cs` (line 33)

This inconsistency makes unit testing harder and could produce subtle bugs if code is tested
with a fake time provider while some paths bypass it.

**Found by:** Comprehensive review — code quality review.

**Recommendation:** For Worker/Email services that already have `TimeProvider` available, use
it consistently in all code paths. For Admin pages, inject `TimeProvider` where time-sensitive
logic is present. Accept the pragmatic tradeoff for simple display-only timestamps.

## Comments
