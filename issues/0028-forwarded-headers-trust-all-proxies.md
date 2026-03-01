---
id: 28
title: "Forwarded headers trust all proxies — IP spoofing possible"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: low
---

## Description

**Severity: Low**

In `Admin/Program.cs` lines 15-20, `KnownNetworks.Clear()` and `KnownProxies.Clear()` means `X-Forwarded-For` and `X-Forwarded-Proto` headers are trusted from any source. An attacker with direct access could spoof their IP to bypass rate limiting or set `X-Forwarded-Proto: https` to trick the app into setting Secure cookies over HTTP.

**Remediation:** Configure `KnownProxies` or `KnownNetworks` to the expected reverse proxy address. Document the trust-all configuration.

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. The Secure cookies claim is **factually wrong**: CookieSecurePolicy.Always is hardcoded (lines 53, 64). The Clear() pattern is Microsoft-recommended for Docker. Default ForwardLimit=1 limits spoofing. Only concrete impact is rate-limiter bypass, which is minor for an authenticated internal tool. Static KnownProxies is impractical in Docker.

### claude — 2026-03-01 (detailed review)

**Verdict: PARTIALLY_VALID — Low severity is correct, but the description contains factual errors and overstates the practical risk.**

#### What the code actually does

In `src/Hpoll.Admin/Program.cs` (lines 14-19):

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
```

This clears the default `KnownNetworks` (which includes `127.0.0.1/8` and `::1`) and `KnownProxies` lists. When both are empty, ASP.NET Core's `ForwardedHeadersMiddleware` trusts `X-Forwarded-For` and `X-Forwarded-Proto` from any source IP.

Importantly, `ForwardLimit` is **not set**, so it retains its default value of **1**. This means only the **last** (rightmost) entry in the `X-Forwarded-For` chain is consumed — the one added by the immediate upstream proxy.

#### Factual errors in the issue description

1. **Secure cookie claim is wrong.** The issue states an attacker could "set `X-Forwarded-Proto: https` to trick the app into setting Secure cookies over HTTP." This is **incorrect**. Both the authentication cookie and the session cookie use `CookieSecurePolicy.Always` (hardcoded at lines 43 and 54 of Program.cs), which sets the `Secure` flag unconditionally regardless of the request scheme. The `X-Forwarded-Proto` header has no effect on Secure cookie behavior in this application.

2. **"Any source" is partially misleading.** While technically the middleware will accept the header from any IP, the `ForwardLimit=1` default means an attacker injecting a multi-hop `X-Forwarded-For` chain (e.g., `X-Forwarded-For: fake-ip, attacker-ip`) only gets the last entry processed. If a legitimate reverse proxy sits in front and appends its own entry, the attacker's spoofed value would be pushed to a position beyond the ForwardLimit and discarded.

#### Concrete impact analysis

The **only** code that uses the client IP is the login rate limiter in `src/Hpoll.Admin/Pages/Login.cshtml.cs` (line 39):

```csharp
var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
```

This IP-keyed rate limiter allows 5 failed attempts per IP before a 15-minute lockout. With forwarded headers trusted from all sources, an attacker who can reach the admin portal directly (bypassing the reverse proxy) could:

- Spoof a different `X-Forwarded-For` value on each batch of 5 attempts to avoid lockout
- Effectively get unlimited brute-force attempts against the login password

However, the realistic risk is low because:

1. **Deployment topology limits exposure.** The `docker-compose.yml` exposes port 8080 directly on the admin container with no Nginx or other reverse proxy configured. In this setup, there is no proxy to trust or distrust — the `RemoteIpAddress` is the actual client IP unless someone manually crafts `X-Forwarded-For` headers. The real concern exists only if the admin is deployed behind a reverse proxy **and** the attacker can bypass that proxy to reach the container directly.

2. **Admin portal is an internal tool.** This is a monitoring dashboard for Philips Hue sensors, not a public-facing application. It is expected to be deployed on a private network or behind authentication at the infrastructure level.

3. **Password-only auth with lockout is already weak.** The rate limiter is an in-memory `ConcurrentDictionary` that resets on app restart. The real defense is password strength (enforced minimum 8 characters, bcrypt-hashed), not IP-based rate limiting.

4. **KnownProxies.Clear() is the Microsoft-recommended pattern for Docker.** The [official ASP.NET Core documentation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer) recommends this exact pattern when the proxy IP is not known in advance, which is the case in Docker environments where container IPs are dynamic.

#### Recommended disposition

**Downgrade to informational / won't-fix.** The issue correctly identifies that the forwarded headers configuration trusts all proxies, but:

- The stated Secure cookie attack vector does not apply
- The rate limiter bypass requires direct container access (bypassing the proxy), which is itself a more serious misconfiguration
- The recommended fix (hardcoding `KnownProxies`) is impractical in Docker and would break legitimate proxy setups
- A more useful improvement would be to add account lockout by credential (not just by IP), or to add fail2ban-style logging, but these are enhancements rather than fixes to the forwarded headers configuration

If any change is warranted, it would be to document that the admin portal should not be exposed to untrusted networks, which is already implied by its deployment model.
