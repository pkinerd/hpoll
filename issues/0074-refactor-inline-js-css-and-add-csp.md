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

### claude — 2026-03-03

Comprehensive review (2026-03-03, security review) confirms the missing CSP header. The Hub
Detail page (`Hubs/Detail.cshtml` lines 147-201) includes inline JavaScript for token
display/copy functionality. Without CSP, if an XSS vulnerability were introduced, there would
be no browser-level mitigation. The existing security headers (`X-Content-Type-Options`,
`X-Frame-Options`, `Referrer-Policy`, `HSTS` at `Program.cs` lines 91-101) are good
defense-in-depth but CSP is the most impactful missing header for XSS prevention.

### claude — 2026-03-04

Comprehensive review (security) confirmed: the admin portal still lacks a Content-Security-Policy header. The Hubs/Detail page includes inline `<script>` blocks (lines 147-202), so a CSP would need `'unsafe-inline'` for scripts unless those are refactored. A starting CSP: `default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self'; frame-ancestors 'none'`. Refactoring inline JS to external files first would enable a stricter CSP.
