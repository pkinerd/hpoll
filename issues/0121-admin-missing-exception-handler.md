---
id: 121
title: "Admin portal lacks UseExceptionHandler middleware for production errors"
status: open
created: 2026-03-02
author: claude
labels: [enhancement]
priority: low
---

## Description

The admin portal (`src/Hpoll.Admin/Program.cs`) does not configure `UseExceptionHandler()` or `UseDeveloperExceptionPage()`. In production, if an unhandled exception occurs, ASP.NET Core's default behavior returns a generic 500 response. While this is relatively safe (no stack trace leak), configuring an explicit error handler page provides a better user experience and consistent error handling.

**Category:** config
**Severity:** low
**Found by:** Security review (comprehensive review 2026-03-02)

### Affected handlers

The Hub Detail page (`src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs`) illustrates the impact. The `OnPostRefreshTokenAsync` and `OnPostTestConnectionAsync` handlers have per-handler try/catch blocks (needed for `HttpRequestException` from Hue API calls), but three database-only POST handlers have no exception handling:

1. **`OnPostToggleStatusAsync`** (lines 54-73): `SaveChangesAsync` without try/catch
2. **`OnPostClearReauthAsync`** (lines 75-90): `SaveChangesAsync` without try/catch
3. **`OnPostDeleteAsync`** (lines 92-108): `SaveChangesAsync` without try/catch

The inconsistency is architecturally explainable — HTTP-calling handlers need per-handler catch for `HttpRequestException`, while `SaveChangesAsync` failures are rare. Adding per-handler try/catch to every database-only handler across the portal is impractical (these use POST-redirect-GET, so returning `Page()` on error requires loading data that `OnGetAsync` normally provides). A global `UseExceptionHandler` middleware is the right solution.

### Recommendation

Add `app.UseExceptionHandler("/Error")` for the production pipeline and create a corresponding `/Error` Razor page. Optionally add `app.UseDeveloperExceptionPage()` for development mode. This provides a generic fallback for all unhandled exceptions, covering database-only handlers without needing per-handler try/catch.

## Comments

### claude — 2026-03-15

Merged details from #181 (Hub Detail toggle/clear-reauth/delete handlers lack exception handling). The three database-only handlers on the Hub Detail page are the primary example of handlers that would benefit from UseExceptionHandler middleware rather than per-handler try/catch.
