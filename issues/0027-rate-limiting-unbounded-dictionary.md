---
id: 27
title: "Rate limiting dictionary has no size bounds (DoS risk)"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: low
---

## Description

**Severity: Low**

The login rate limiter in `Login.cshtml.cs` line 15 uses a `static ConcurrentDictionary` that:
1. Resets on application restart
2. Has no maximum size — an attacker can grow it unboundedly from many IPs
3. Only works for single-instance deployments

**Remediation:** Add a maximum size check (evict oldest entries if >10,000). For production hardening, consider `System.Threading.RateLimiting` or a distributed store.

## Comments
