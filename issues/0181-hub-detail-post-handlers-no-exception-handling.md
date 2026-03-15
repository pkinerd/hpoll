---
id: 181
title: "Hub Detail toggle/clear-reauth/delete handlers lack exception handling"
status: open
created: 2026-03-15
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

The Hub Detail page's `OnPostRefreshTokenAsync` and `OnPostTestConnectionAsync` handlers have try/catch blocks with `HttpRequestException` and general `Exception` handling, providing user-facing error messages via `ErrorMessage`. Three other POST handlers on the same page have no exception handling:

1. **`OnPostToggleStatusAsync`** (lines 54-73): Calls `SaveChangesAsync` without try/catch
2. **`OnPostClearReauthAsync`** (lines 75-90): Calls `SaveChangesAsync` without try/catch
3. **`OnPostDeleteAsync`** (lines 92-108): Calls `SaveChangesAsync` without try/catch

The inconsistency is partially explainable: the refresh/test-connection handlers make outbound HTTP calls to the Hue API, where `HttpRequestException` is a common and expected failure mode. The toggle/clear-reauth/delete handlers only perform local database operations via `SaveChangesAsync`, where failures are rare. However, if `SaveChangesAsync` does fail (e.g., database locked by backup VACUUM, disk full, or concurrent access), these handlers will throw an unhandled exception that surfaces as a raw error page.

**Location:** `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs`, lines 54-108

**Category:** error-handling consistency

**Severity:** low — `SaveChangesAsync` failures on SQLite are rare in practice. The inconsistency is architecturally explainable (HTTP-calling vs database-only handlers). Addressing #121 (UseExceptionHandler middleware) would provide a generic fallback for all unhandled exceptions, making per-handler try/catch less critical.

**Recommendation:** The preferred fix is to address #121 first (UseExceptionHandler middleware), which provides a generic error page for all unhandled exceptions across the admin portal. This is more maintainable than adding per-handler try/catch to every database-only handler. If per-handler error messages are desired, note that these handlers use the POST-redirect-GET pattern (returning `RedirectToPage`), so switching to `Page()` on error would require loading the hub data that `OnGetAsync` normally provides.

## Comments
