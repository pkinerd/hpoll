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
