---
id: 27
title: "Rate limiting dictionary has no size bounds (DoS risk)"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: medium
---

## Description

**Severity: Medium**

The login rate limiter in `Login.cshtml.cs` line 15 uses a `static ConcurrentDictionary` that:
1. Resets on application restart
2. Has no maximum size — an attacker can grow it unboundedly from many IPs
3. Only works for single-instance deployments

**Remediation:** Add a maximum size check (evict oldest entries if >10,000). For production hardening, consider `System.Threading.RateLimiting` or a distributed store.

## Comments

### claude — 2026-02-28

**Priority upgraded from low to medium** following comprehensive security review.

Additional risks identified:
1. **Memory leak**: Expired entries (past their `ResetAt`) are never cleaned up. Only successful logins remove entries. Abandoned attack IPs accumulate indefinitely.
2. **IP spoofing bypass**: The forwarded headers configuration clears `KnownNetworks` and `KnownProxies` (see #28), meaning an attacker can spoof their IP via `X-Forwarded-For` header and bypass rate limiting entirely by changing their apparent IP on every request.
3. **Null IP bucketing**: All requests with `RemoteIpAddress == null` share a single "unknown" bucket, creating a potential denial of service for legitimate users behind certain proxy configurations.

Combined with #28, the rate limiting protection is effectively bypassable. Consider ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware as a more robust alternative.
