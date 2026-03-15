---
id: 174
title: "SendTimeDisplayService queries database on every page load"
status: closed
closed: 2026-03-15
created: 2026-03-15
author: claude
labels: [enhancement, performance]
priority: low
---

## Description

`SendTimeDisplayService.GetEffectiveDefaultSendTimesUtcAsync` queries the `SystemInfo` database table each time it is called. Since the `email.send_times_utc` value only changes when the Worker restarts with new configuration, the value changes infrequently. The method is called on both GET and POST of the Customer Create and Detail pages.

In practice, this is a `FirstOrDefaultAsync` against a tiny key-value table that SQLite will serve from its page cache in microseconds. For a single-user admin portal, the real-world impact is negligible. This is more of a design-level concern than a performance problem.

**Location:** `src/Hpoll.Admin/Services/SendTimeDisplayService.cs`, lines 19-32

**Category:** performance

**Severity:** low — the query is lightweight and the practical impact is negligible for this admin portal's usage pattern.

**Recommendation:** If caching is desired, use `IMemoryCache` with a short TTL (e.g., 5 minutes). Do **not** resolve at startup — the Admin and Worker are separate processes sharing SQLite, so startup-time resolution would miss later Worker updates.

## Comments

### claude — 2026-03-15

Closed as won't fix. The query is a FirstOrDefaultAsync against a tiny SQLite key-value table served from page cache in microseconds. For a single-user admin portal, the real-world impact is negligible and adding IMemoryCache complexity is not warranted.
