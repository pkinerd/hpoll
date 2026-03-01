---
id: 44
title: "Silent catch blocks swallow JSON parsing exceptions without logging"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [bug, code-quality]
priority: high
---

## Description

**Severity: High**

Multiple locations use bare `catch { }` or `catch { return false; }` with no logging when parsing `DeviceReading.Value` JSON:

- `EmailRenderer.cs` lines 88-89, 96-98, 103-105, 152 — motion/temperature/battery JSON parsing
- `Customers/Detail.cshtml.cs` lines 215-216, 223-225, 230-232 — identical duplicated pattern
- `Hubs/OAuthCallback.cshtml.cs` line 137 — `GetDevicesAsync` exception swallowed entirely

If `DeviceReading.Value` contains malformed JSON (realistic after a schema change, data migration, or bug), these errors are completely invisible. The email summary and activity dashboard would silently show incorrect data (zeros) with no indication of the underlying problem.

**Remediation:** Replace bare `catch` blocks with `catch (Exception ex) { _logger.LogWarning(ex, "..."); }` at minimum. Better yet, extract JSON parsing into typed accessor methods (see #11) that centralize error handling.

**Related:** #11 (extract JSON parsing)

## Comments

### claude — 2026-03-01

**Comprehensive review update:** Additional bare `catch { }` blocks found beyond the locations already noted:

- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs` lines 214-216, 223-225, 229-231 — motion, temperature, and battery JSON parsing in `LoadActivitySummaryAsync`
- `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs` lines 137-139 — silently swallows any failure during the connectivity test after hub registration

These catch **all** exceptions including `OutOfMemoryException`, `StackOverflowException`, etc. All should be narrowed to `catch (JsonException)` or the specific expected exception type.
