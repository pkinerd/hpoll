---
id: 96
title: "Token reveal endpoint uses cacheable GET request"
status: open
created: 2026-03-02
author: claude
labels: [security]
priority: low
---

## Description

The `OnGetTokenAsync` handler in `Hubs/Detail.cshtml.cs` (lines 35-51) returns raw OAuth token values (access token, refresh token, application key) as JSON responses to **GET** requests.

While the endpoint is behind authentication and called via `fetch()` from JavaScript (not direct navigation), the response lacks `Cache-Control: no-store` headers. This means intermediate caches or browser heuristic caching could theoretically retain the response, though the practical risk is low given HTTPS-only cookies and authentication requirements.

**Note:** Issue #0024 addressed tokens embedded in HTML page source (closed). The current AJAX approach is a significant improvement. This finding is about adding cache-control headers as a defense-in-depth measure.

**Mitigating factors:**
- The endpoint requires authentication (all Razor Pages use `RequireAuthorization()`)
- Token values are in the response body, not the URL — proxy logs would not capture them
- The endpoint is called via `fetch()`, not direct navigation — it won't appear in browser history
- The application uses HTTPS-only cookies and HSTS

**Recommendation:**
Add `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` to the `OnGetTokenAsync` handler. This is a one-line fix that adds appropriate `Cache-Control: no-store` headers.

Note: Switching from GET to POST would also require antiforgery token plumbing in the fetch call, making it a more invasive change for marginal benefit.

## Comments
