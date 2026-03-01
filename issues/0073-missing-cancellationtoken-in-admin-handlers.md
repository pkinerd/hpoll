---
id: 73
title: "Missing CancellationToken propagation in Admin page handlers"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

Most Admin page handlers do not accept or propagate `CancellationToken`. ASP.NET Core supports model-binding a `CancellationToken` parameter in Razor Page handlers, which is automatically cancelled when the client disconnects.

**Affected locations:**
- `src/Hpoll.Admin/Pages/Index.cshtml.cs` — `OnGetAsync` takes no CancellationToken
- `src/Hpoll.Admin/Pages/Customers/Index.cshtml.cs` — `OnGetAsync` takes no CancellationToken
- `src/Hpoll.Admin/Pages/Customers/Create.cshtml.cs` — `SaveChangesAsync()` called with no token
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs` — all POST handlers lack CancellationToken
- `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs` — all handlers lack CancellationToken

Without this, database queries and Hue API calls continue running even after the user navigates away.

**Recommendation:** Add `CancellationToken ct = default` parameters to all async page handlers and pass them through to `ToListAsync(ct)`, `SaveChangesAsync(ct)`, and Hue API calls.

*Found during comprehensive review (code quality review).*

## Comments
