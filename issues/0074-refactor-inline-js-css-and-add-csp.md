---
id: 74
title: "Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header"
status: open
created: 2026-03-01
author: claude
labels: [security, enhancement]
priority: low
---

## Description

Follow-up from #42 (security response headers). The basic headers (X-Content-Type-Options, X-Frame-Options, Referrer-Policy) were added in commit 034a6fd, but Content-Security-Policy (CSP) was intentionally deferred because the admin portal's Razor pages use inline JavaScript and CSS that would break under a strict CSP policy.

### Current blockers for CSP

**`Hubs/Detail.cshtml`** contains:
- An inline `<script>` block (~50 lines of JavaScript for token show/copy/fetch)
- Seven `onclick="..."` inline event handlers on token show/copy buttons
- One `onsubmit="return confirm(...)"` inline handler on the delete form

**`Shared/_Layout.cshtml`** contains:
- A large inline `<style>` block (~60 lines of CSS)

A CSP with `script-src 'self'` or `style-src 'self'` would immediately break both pages.

### Implementation approach

1. **Extract inline CSS** from `_Layout.cshtml` into a static stylesheet in `wwwroot/css/`
2. **Extract inline JS** from `Hubs/Detail.cshtml` into a static script file in `wwwroot/js/`
3. **Replace all `onclick`/`onsubmit` inline handlers** with `addEventListener` calls in the extracted JS file
4. **Add CSP middleware** to `Program.cs` alongside the existing security headers:
   ```csharp
   ctx.Response.Headers["Content-Security-Policy"] =
       "default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors 'none'";
   ```
5. **Verify** that all admin portal pages render and function correctly under the new CSP

### Why not use workarounds

- `'unsafe-inline'` in `script-src` defeats the purpose of CSP for XSS mitigation
- Nonce-based CSP (`script-src 'nonce-xxx'`) adds significant middleware complexity for per-request nonce generation and Razor injection — overkill for this portal

### Context

The actual XSS risk is low for this admin portal (authenticated, no user-generated content, internal network), but CSP is a valuable defense-in-depth measure and a best practice.

**Related:** #42 (closed — basic security headers added)

## Comments
