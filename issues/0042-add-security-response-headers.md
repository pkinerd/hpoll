---
id: 42
title: "Add security response headers (CSP, X-Frame-Options, HSTS)"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: high
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
