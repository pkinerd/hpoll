---
id: 85
title: "Cookie authentication lacks absolute session expiration"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: low
---

## Description

The authentication cookie is configured with `ExpireTimeSpan = TimeSpan.FromHours(24)` in
`Program.cs` (lines 45-54). There is no explicit `SlidingExpiration` setting (defaults to
`true` in ASP.NET Core), meaning the cookie is automatically renewed on each request,
potentially keeping a session alive indefinitely.

Issues:
- No absolute session timeout — active sessions never expire
- No session invalidation mechanism when the admin password hash changes
- No way to force logout all sessions

**Found by:** Comprehensive review — security review.

**OWASP reference:** A07:2021-Identification and Authentication Failures

**Recommendation:** Set `SlidingExpiration = false` or add an absolute session timeout.
Implement a mechanism to invalidate sessions when the admin password hash changes (e.g.,
by incorporating a token version in the claims).

## Comments
