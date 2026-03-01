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

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID — the dictionary is technically unbounded, but the practical risk is significantly overstated. Priority should remain low, not medium.**

**1. Where is the rate limiting dictionary?**

The dictionary is at `src/Hpoll.Admin/Pages/Login.cshtml.cs` line 15:
```csharp
private static readonly ConcurrentDictionary<string, (int Count, DateTime ResetAt)> _failedAttempts = new();
```
It is keyed by IP address string, with a value tuple of `(int Count, DateTime ResetAt)`.

**2. Is it actually unbounded?**

Technically yes — there is no explicit cap on the number of entries. However, the issue overstates the practical severity for several reasons:

- **Antiforgery tokens gate POST requests.** ASP.NET Core Razor Pages enforce antiforgery token validation on all POST handlers by default (`Program.cs` line 68 configures `AddAntiforgery`, and the login form uses `<form method="post" asp-page="/Login">` which auto-generates the token via Razor tag helpers). An attacker cannot simply fire raw HTTP POSTs from millions of IPs — each request needs a valid antiforgery cookie+token pair, which requires first making a GET to the login page. This raises the cost of a mass-IP dictionary-stuffing attack considerably.

- **Entries ARE removed in two places** (lines 49 and 64):
  - Line 49: When a locked-out IP's `ResetAt` time has passed and they attempt login again, the entry is removed via `TryRemove`.
  - Line 64: On successful login, the entry is removed.

  The prior comment's claim of a "memory leak" is misleading. Expired entries are lazily cleaned up on the next login attempt from that IP. The dictionary does not grow without bound during normal operation — it only retains entries for IPs that triggered lockout and never returned.

- **Memory impact is negligible.** Each dictionary entry consists of: a string key (an IPv4 address is ~15 chars = ~46 bytes on the heap including object header, or IPv6 ~39 chars = ~94 bytes), plus a value tuple of `(int, DateTime)` = 12 bytes, plus dictionary overhead per entry (~50-80 bytes). Conservatively, each entry is ~140-180 bytes. Even 100,000 unique attacker IPs (an extreme scenario for an internal admin portal) would consume roughly 14-18 MB — well within normal operating margins.

**3. What is the realistic DoS risk for this internal admin portal?**

Very low. This is an admin portal for managing Philips Hue bridges (`docker-compose.yml` exposes it on port 8080). It is:
- A single-purpose internal tool, not a public-facing web application.
- Deployed via Docker Compose with a single admin container — the attack surface is limited to whoever can reach port 8080.
- Protected by antiforgery tokens, meaning an attacker needs browser-like interaction (GET + extract token + POST) per attempt, not just raw POST floods.
- The dictionary only grows from *failed* login attempts from *distinct* IPs. A single attacker from one IP adds exactly one entry regardless of how many attempts they make.

An attacker would need to control hundreds of thousands of distinct IPs, each performing the GET-then-POST antiforgery dance, to cause meaningful memory pressure. This is a sophisticated distributed attack against what is effectively an internal monitoring tool.

**4. Would the dictionary actually grow unbounded in practice?**

No. In realistic usage:
- A handful of admin users generate zero entries (successful logins remove entries).
- Occasional brute-force attempts from a few IPs add a few entries that are cleaned up on the next attempt after lockout expires.
- The application restarts periodically (container updates, deployments), which clears the dictionary entirely.

The only scenario where entries accumulate is abandoned lockout entries from IPs that never return. Over months of uptime with no restarts, this might reach hundreds or low thousands of entries — a trivial amount.

**5. Is a MemoryCache replacement genuinely necessary?**

No. Replacing `ConcurrentDictionary` with `MemoryCache` adds complexity (sliding expiration semantics, cache eviction policies, dependency on `IMemoryCache` registration) for negligible benefit in this context. The simpler approach, if any change is warranted, would be adding a periodic cleanup of expired entries (a `Timer` that runs every 30 minutes to remove entries where `ResetAt < DateTime.UtcNow`). But even this is optional given the practical constraints above.

**6. Assessment of the prior comment's additional risks:**

- **"Memory leak"**: Incorrect terminology. Expired entries are reclaimable on next access (line 49). The correct term is "lazy cleanup" — entries linger until their IP returns. This is a common and acceptable pattern.
- **"IP spoofing bypass" via X-Forwarded-For**: This is a legitimate concern, but it is issue #28's scope, not this issue's. Also, spoofing X-Forwarded-For still requires passing antiforgery validation, which requires a valid session cookie.
- **"Null IP bucketing"**: Minor concern. In practice, `RemoteIpAddress` is null only in unusual proxy configurations. All such requests sharing a single "unknown" bucket means they share a single lockout counter — at most this causes a false lockout for a few users, not a DoS.
- **Priority upgrade to medium**: Unjustified. The original low priority was correct. The attack scenario requires a sophisticated distributed attacker targeting an internal admin tool, bypassing antiforgery tokens, from enough IPs to cause memory pressure. This is not a medium-severity finding.

**Recommendation:** Keep priority at **low**. If desired, add a simple periodic expired-entry cleanup timer as a minor hardening measure. A full migration to `Microsoft.AspNetCore.RateLimiting` or `MemoryCache` is over-engineering for this use case.
