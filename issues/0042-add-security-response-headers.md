---
id: 42
title: "Add security response headers (CSP, X-Frame-Options, HSTS)"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: medium
---

## Description

**Severity: High**

The admin portal middleware pipeline (`Program.cs` lines 73-97) does not set any security response headers:

- **No `Content-Security-Policy`** — Especially critical because OAuth tokens are embedded in page JavaScript (see #24). Without CSP, any injected inline script can exfiltrate tokens.
- **No `X-Content-Type-Options: nosniff`** — Allows MIME-type sniffing attacks.
- **No `X-Frame-Options: DENY`** — The admin portal is vulnerable to clickjacking attacks where an attacker embeds it in an iframe.
- **No `Strict-Transport-Security`** — No HSTS header even when behind a TLS-terminating reverse proxy.
- **No `Referrer-Policy`** — Referrer leakage could expose internal URLs.
- **No `Permissions-Policy`** — No restriction on browser feature access.

**Remediation:** Add a middleware to set security headers:
```csharp
app.Use(async (ctx, next) => {
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'";
    await next();
});
```

**Related:** #24 (tokens in HTML), #26 (cookie security)

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded high->medium. Primary justification is **outdated**: tokens no longer in page HTML (issue #24 closed, commit 07ba669). Proposed CSP script-src 'self' would **break the app** (Detail.cshtml has inline script and onclick handlers). HSTS inappropriate for HTTP-only container. The valid low-effort items are X-Content-Type-Options: nosniff and X-Frame-Options: DENY.

### claude — 2026-03-01 (detailed review)

**Verdict: PARTIALLY_VALID — priority should remain medium, severity should be downgraded from High to Medium.**

#### Finding-by-finding analysis

**1. CSP claim is overstated and the proposed fix would break the app.**

The issue's primary urgency argument — "Especially critical because OAuth tokens are embedded in page JavaScript (see #24)" — is **stale**. Issue #24 was closed via commit 07ba669 which moved tokens to lazy-load via an authenticated AJAX endpoint (`fetchToken()`). Tokens are no longer in the page source.

More importantly, the proposed CSP `script-src 'self'` would **immediately break** `Hubs/Detail.cshtml`, which relies on:
- An inline `<script>` block (~50 lines of JavaScript for token show/copy/fetch)
- Seven `onclick="..."` inline event handlers on token show/copy buttons
- One `onsubmit="return confirm(...)"` inline handler on the delete form

To make CSP work, the implementation would need to either:
- Move all inline JS to a separate `.js` file and rewrite all `onclick`/`onsubmit` handlers to use `addEventListener` — a non-trivial refactor
- Use `'unsafe-inline'` in `script-src`, which defeats the purpose of CSP for XSS mitigation
- Use nonce-based CSP (`script-src 'nonce-xxx'`), requiring middleware to generate per-request nonces and inject them into Razor output — significant complexity

The `_Layout.cshtml` also uses a large inline `<style>` block (~60 lines of CSS), so `style-src 'self'` would break the layout too. The entire CSS is inline, not in a separate stylesheet.

**This is not a drop-in header; it requires refactoring the frontend first.** The issue should acknowledge this prerequisite work.

**2. X-Content-Type-Options: nosniff — Valid, trivial to add.**

This is a single-line middleware addition with zero risk of breaking anything. However, the actual attack surface is minimal: the admin portal serves only Razor-rendered HTML and static files from `wwwroot` (via `UseStaticFiles()`). MIME-sniffing attacks are primarily a concern when serving user-uploaded content, which this portal does not do. Still worth adding as defense-in-depth — no cost, no risk.

**3. X-Frame-Options: DENY — Valid, trivial to add.**

Clickjacking protection is straightforward and appropriate. The admin portal is an authenticated internal tool and should never be framed. This is the strongest justified item in the issue. Note: the modern replacement is `frame-ancestors 'none'` in CSP, but since CSP itself is complex to add (see above), `X-Frame-Options: DENY` is the pragmatic choice.

**4. Strict-Transport-Security (HSTS) — Inappropriate for the container.**

The admin container listens on HTTP port 8080 (`ASPNETCORE_URLS=http://+:8080` in Dockerfile.admin). There is no TLS termination in the container itself, and no reverse proxy is configured in `docker-compose.yml`. Adding HSTS at the app level would be incorrect — HSTS should be configured on the TLS-terminating reverse proxy (e.g., nginx, Traefik, Cloudflare) in front of the app, not on the app itself. ASP.NET Core's built-in `UseHsts()` is designed for apps that terminate TLS directly.

If the deployment does use a reverse proxy, HSTS should be configured there. The issue does not acknowledge this and implies the app itself should emit the header, which would be misleading for HTTP-only traffic.

**5. Referrer-Policy — Low value for this app.**

The admin portal is an internal tool with no external links except the Hue OAuth URL (which uses `rel="noopener"`). Referrer leakage is not a meaningful threat. The internal URLs themselves (e.g., `/Customers/Detail/3`) reveal nothing sensitive. Adding it is harmless but the "could expose internal URLs" framing overstates the risk.

**6. Permissions-Policy — Negligible value.**

This portal does not use any browser APIs (camera, microphone, geolocation, etc.). Adding `Permissions-Policy` is pure ceremony for this use case.

#### Risk assessment for an internal admin portal

The issue frames this as "Severity: High" but the actual threat model is limited:
- **Authentication required:** All pages require cookie auth (`RequireAuthorization()`)
- **No user-generated content:** No XSS vectors from stored user input rendered in pages
- **Internal network:** Typically deployed behind a firewall/VPN
- **No sensitive data in page source:** Tokens are fetched via AJAX only when explicitly requested

The realistic attack scenario for clickjacking or MIME-sniffing against an authenticated internal admin portal behind cookie auth with `SameSite=Lax` is very narrow.

#### Recommendation

**Keep as medium priority.** The actionable items are:

1. **Do now (trivial):** Add `X-Content-Type-Options: nosniff` and `X-Frame-Options: DENY` as a simple middleware — 5 minutes of work, zero risk.
2. **Defer CSP:** File a separate issue to refactor inline JS/CSS out of Razor pages first, then add CSP as a follow-up. Do not attempt to add CSP without this prerequisite.
3. **Skip HSTS:** Document that HSTS belongs on the reverse proxy, not in the app container.
4. **Optional:** Add `Referrer-Policy: strict-origin-when-cross-origin` — harmless, near-zero value for this app.

The issue description should be updated to remove the stale #24 reference and acknowledge the CSP prerequisite work.
