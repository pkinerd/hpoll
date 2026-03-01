---
id: 63
title: "Add HTTPS redirect middleware for admin portal in production"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: medium
---

## Description

The admin portal does not call `app.UseHttpsRedirection()` in `Program.cs`. The Docker container listens on plain HTTP (port 8080). If the admin portal is accessed without a TLS-terminating reverse proxy, all traffic — including authentication cookies and session tokens — is transmitted in cleartext.

This is distinct from issue #0042 (security response headers). `UseHttpsRedirection` is a middleware that sends HTTP 307 redirects to upgrade HTTP requests to HTTPS, while HSTS is a response header that tells browsers to always use HTTPS.

**File:** `src/Hpoll.Admin/Program.cs`

**Recommended fix:**
1. Add `app.UseHttpsRedirection()` and `app.UseHsts()` conditionally in production
2. The `ForwardedHeaders` middleware is already configured, which is good for proxy setups
3. Document prominently that a reverse proxy with TLS termination is required for production deployment

**Related:** Issue #0042 (security response headers), issue #0026 (cookie SecurePolicy)

**Source:** Security review finding S4.1

## Comments
