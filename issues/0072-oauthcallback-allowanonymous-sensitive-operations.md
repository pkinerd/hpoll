---
id: 72
title: "OAuthCallback page is AllowAnonymous but performs sensitive operations"
status: closed
created: 2026-03-01
author: claude
labels: [security]
priority: medium
---

## Description

The `OAuthCallbackModel` is decorated with `[AllowAnonymous]` (`OAuthCallback.cshtml.cs` line 11), making it accessible without authentication. While an OAuth callback must be reachable for the redirect flow, this page performs sensitive operations:

1. Exchanges authorization codes for OAuth tokens
2. Enables the link button on the Hue Bridge
3. Registers applications on the bridge
4. Creates/updates Hub entities with tokens in the database

The session-based CSRF state validation (lines 55-62) provides some protection, but:
- It depends on the admin being logged in to have initiated the flow (session must exist)
- An attacker who observes the OAuth redirect URL could potentially extract or guess the state parameter
- If the session expires during the OAuth flow, the CSRF check would fail with a confusing error

**OWASP reference:** A01:2021-Broken Access Control

**Recommendation:**
- Add an explicit authentication check before proceeding with token exchange and hub registration
- If the session-based CSRF check fails due to expired session, redirect to login with a clear error
- Validate that `expectedCsrf` is non-null before comparing

This is related to but distinct from issue #0065 (signed state parameter), which addresses the CSRF mechanism itself. This issue addresses the broader access control concern.

*Found during comprehensive review (security review).*

## Comments

### claude — 2026-03-01

**Closed:** Consolidated into #0065 (OAuth callback should use signed state parameter). The signed state approach embeds the authenticated user's identity, which inherently validates the request originated from an authenticated admin session — resolving this issue's access control concern alongside #0065's CSRF mechanism improvement.
