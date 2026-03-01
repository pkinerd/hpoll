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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Scope should be narrowed. POST handlers should NOT be cancelled on disconnect (data inconsistency risk). SQLite queries are near-instantaneous for this dataset. HttpClient already has timeout configured. Focus should be on the 3 handlers making Hue API calls and the one expensive read (LoadActivitySummaryAsync), not all 20 async handlers.

### critical-review — 2026-03-01

**Verdict: PARTIALLY_VALID — Low priority, significantly over-scoped. Recommend closing or narrowing to a targeted fix.**

**Factual accuracy of the issue claims:**

1. **"Most Admin page handlers do not accept or propagate CancellationToken" — TRUE.** Confirmed across all handler files: `Index.cshtml.cs`, `Customers/Index.cshtml.cs`, `Customers/Create.cshtml.cs`, `Customers/Detail.cshtml.cs`, `Hubs/Detail.cshtml.cs`, `Hubs/OAuthCallback.cshtml.cs`, and `About.cshtml.cs`. Zero handlers accept a CancellationToken parameter or use `HttpContext.RequestAborted`.

2. **"ASP.NET Core supports model-binding a CancellationToken parameter" — TRUE.** ASP.NET Core does automatically bind a `CancellationToken` parameter in handler methods to `HttpContext.RequestAborted`. This is a legitimate framework feature.

3. **"Database queries and Hue API calls continue running even after the user navigates away" — MISLEADING.** This statement is technically true but significantly overstates the practical impact.

**Impact analysis — why this is mostly noise for a low-traffic admin portal:**

- **SQLite queries are sub-millisecond.** The database is local SQLite with a small dataset (a handful of customers, hubs, devices). Every `CountAsync`, `FirstOrDefaultAsync`, `ToListAsync`, and `SaveChangesAsync` call completes in under a millisecond. Cancelling these would save essentially zero resources. The `LoadActivitySummaryAsync` method in `Customers/Detail.cshtml.cs` (lines 253-339) runs 5 sequential queries including a potentially larger `DeviceReadings` query, but even this is bounded by `windowHours * windowCount` hours of data in a local SQLite file.

- **POST handlers MUST NOT be cancelled on disconnect.** The issue recommends adding CancellationToken to "all POST handlers" including `OnPostUpdateNameAsync`, `OnPostUpdateEmailsAsync`, `OnPostToggleStatusAsync`, `OnPostDeleteAsync`, etc. Cancelling these mid-write risks data inconsistency — a name could be updated but `UpdatedAt` not set, or a hub could be partially modified. The `OAuthCallback.OnGetAsync` performs a multi-step registration flow (exchange code, enable link button, register app, get bridge ID, save to DB) where partial cancellation would leave the system in a broken state.

- **Hue API calls already have HTTP timeout protection.** The `IHueApiClient` methods (`RefreshTokenAsync`, `GetDevicesAsync`, `ExchangeAuthorizationCodeAsync`, etc.) already accept `CancellationToken ct = default` but are called through `HttpClient` which has configured timeouts. A disconnected admin user does not cause resource exhaustion — the HTTP call finishes or times out on its own.

- **This is an internal admin portal, not a public-facing API.** The portal is behind authentication (`Login.cshtml.cs` with password hash verification, rate limiting). Traffic is a single admin user. The scenario where "user navigates away during a query" causing resource waste is practically non-existent.

**What would actually be worth doing (narrow scope):**

At most, adding CancellationToken to the two handlers that make external Hue API calls would be defensible:
- `Hubs/Detail.cshtml.cs` — `OnPostRefreshTokenAsync` (calls `RefreshTokenAsync`)
- `Hubs/Detail.cshtml.cs` — `OnPostTestConnectionAsync` (calls `GetDevicesAsync`)

These are GET-like operations where cancellation is safe and the external call could theoretically take seconds. But even here, the `HttpClient` timeout already provides a ceiling.

**Adding CancellationToken to all ~15 async handlers and ~30 EF Core calls is pure code noise** that adds visual clutter for zero practical benefit. Every handler signature grows, every EF call gets an extra parameter, and the codebase becomes harder to read — all to handle a scenario that essentially never occurs in a single-user admin tool backed by local SQLite.

**Recommendation:** Close this issue as won't-fix, or drastically narrow to only the 2 external API call handlers if the team has a strict "propagate cancellation everywhere" policy. The current blanket recommendation is not worth the code noise.
