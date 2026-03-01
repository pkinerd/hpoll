---
id: 81
title: "Login rate-limiting lockout path is untested (security-critical)"
status: open
created: 2026-03-01
author: claude
labels: [testing, security]
priority: medium
---

## Description

The login rate-limiting lockout mechanism at `Login.cshtml.cs` lines 42-49 is security-critical
but has 0% coverage:

- The lockout check (when a client IP exceeds `MaxAttempts` = 5) at lines 44-47
- The "Too many failed attempts" error return
- The lockout expiry cleanup at line 49
- The `returnUrl` redirect on successful login at line 75

This code protects against brute-force password attacks and is a key security control.
Line coverage for Login.cshtml.cs is 89.6% but branch coverage is only 65.4% (17/26).

**Found by:** Comprehensive review — code coverage analysis and unit testing review.

**Recommendation:** Add tests that:
1. Submit 5+ failed login attempts from the same IP and verify lockout message
2. Verify lockout expires after the duration window
3. Test successful login with a `ReturnUrl` query parameter

## Comments
