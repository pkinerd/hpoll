---
id: 65
title: "OAuth callback should use signed state parameter instead of session-based CSRF"
status: open
created: 2026-03-01
author: claude
labels: [security, enhancement]
priority: medium
---

## Description

The OAuth callback endpoint (`OAuthCallback.cshtml.cs`) is `[AllowAnonymous]` and relies on an in-memory session-stored CSRF token (`OAuthCsrf`) for validation. This has several limitations:

1. **Session dependency:** If the session expires (30-minute timeout) between OAuth initiation and callback, the CSRF check fails and the authorization code is wasted
2. **Multi-instance incompatibility:** The distributed memory cache is per-instance, so in a multi-instance deployment, the callback could be routed to a different instance that lacks the session state
3. **Session fixation risk:** The CSRF token's security depends entirely on the session infrastructure

**File:** `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs:48-61`

**Recommended fix:**
- Use a cryptographically signed state parameter (e.g., using ASP.NET Data Protection API) that embeds the customer ID and a timestamp
- Validate the signature and timestamp on callback instead of looking up session state
- This eliminates the session dependency and works across multiple instances
- Add an explicit expiration (e.g., 10 minutes) to the signed state

**OWASP reference:** A07:2021-Identification and Authentication Failures

**Source:** Comprehensive review -- security review finding

## Comments
