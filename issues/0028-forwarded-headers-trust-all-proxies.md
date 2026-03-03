---
id: 28
title: "Forwarded headers trust all proxies — IP spoofing possible"
status: closed
created: 2026-02-28
closed: 2026-03-03
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

### claude — 2026-03-01 (independent critical review)

**Verdict: PARTIALLY_VALID — agree with prior reviewer. Recommend close as won't-fix.**

#### Independent verification of prior review findings

All claims in the detailed review above have been independently verified against the `main` branch code:

1. **CookieSecurePolicy.Always hardcoded** -- Confirmed. Both `options.Cookie.SecurePolicy = CookieSecurePolicy.Always` settings (authentication cookie and session cookie) unconditionally set the `Secure` flag. The `X-Forwarded-Proto` header cannot influence this behavior. The issue's claim about "trick the app into setting Secure cookies over HTTP" is **factually incorrect**.

2. **ForwardLimit defaults to 1** -- Confirmed. No explicit `ForwardLimit` is set in the configuration, so ASP.NET Core's default of 1 applies. This means only the rightmost `X-Forwarded-For` entry is consumed. When a legitimate reverse proxy is present, it would overwrite any attacker-injected value.

3. **IP usage is limited to login rate limiting** -- Confirmed. A `git grep` across the entire `src/Hpoll.Admin/` tree shows `HttpContext.Connection.RemoteIpAddress` is referenced only in `Login.cshtml.cs` (line 39) for the in-memory rate limiter (`ConcurrentDictionary` keyed by IP, 5 attempts / 15-minute lockout).

#### Additional finding: Request.Scheme usage in OAuthCallback

The prior review did not note that `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs` (line 78) constructs a callback URL using `HttpContext.Request.Scheme`:

```csharp
var callbackUrl = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/Hubs/OAuthCallback";
```

The `X-Forwarded-Proto` header does influence `Request.Scheme` when forwarded headers are trusted. However, the OAuth initiation in `Customers/Detail.cshtml.cs` uses a **configuration-based** `_hueApp.CallbackUrl` for the `redirect_uri` parameter. If an attacker spoofed `X-Forwarded-Proto: http` during the callback phase, the reconstructed URL would mismatch the registered redirect URI, causing the Hue API token exchange to **fail** (a self-inflicted denial-of-service). This is not a privilege escalation or data exposure vector -- it would only break the attacker's own OAuth flow.

#### Assessment of the proposed remediation

The issue recommends configuring `KnownProxies` or `KnownNetworks` to restrict which proxies are trusted. This recommendation has practical problems:

- **Docker networking is dynamic.** Container IPs change on restart, network recreation, and scaling. Hardcoding proxy IPs would create a fragile configuration that breaks silently.
- **The `Clear()` pattern is Microsoft's documented recommendation** for containerized environments where the proxy IP is not known at build time.
- **No reverse proxy is configured in `docker-compose.yml`.** The admin container exposes port 8080 directly. There is currently no proxy to trust or restrict. The forwarded headers configuration is a forward-looking default for users who add their own reverse proxy.

A more practical improvement (if any) would be to add a configurable `TrustedProxies` setting that defaults to empty (trust-all, current behavior) but can be set by operators who deploy behind a known proxy. However, this is an enhancement, not a security fix.

#### Conclusion

The issue correctly identifies that `KnownNetworks.Clear()` and `KnownProxies.Clear()` cause the middleware to accept forwarded headers from any source. However:

- One of the two claimed attack vectors (Secure cookie bypass) does not apply at all
- The remaining vector (rate limiter bypass) requires direct network access to the container, which is itself a deployment misconfiguration
- The in-memory rate limiter is already weak by design (resets on restart) and is not the primary security control
- The proposed fix is impractical for the project's Docker deployment model
- The priority of "low" is appropriate but "informational/won't-fix" may be more accurate

**Recommendation: Close as won't-fix.** The trust-all configuration is intentional and appropriate for the deployment model. Document that the admin portal should be deployed on a trusted network or behind infrastructure-level access controls.

### claude (critical review) — 2026-03-01

**Assessment: PARTIALLY_VALID — agree with prior reviews. Adding one correction and a consolidated impact table.**

Prior reviews correctly identified the Secure cookie claim as wrong and the rate-limiter bypass as requiring unrealistic prerequisites. One inaccuracy shared by all prior reviews needs correction.

**Correction: the antiforgery cookie lacks `SecurePolicy.Always`.**

In `src/Hpoll.Admin/Program.cs` lines 68-71:
```csharp
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
});
```

Unlike the auth cookie (line 53) and session cookie (line 64), the antiforgery cookie does NOT set `SecurePolicy`. It defaults to `CookieSecurePolicy.None`, meaning its `Secure` flag IS influenced by `HttpContext.Request.IsHttps`, which depends on `X-Forwarded-Proto`. Prior reviews stated all cookies are hardcoded to `Always` — this is not fully accurate.

In practice this has negligible impact: spoofing `X-Forwarded-Proto: https` would cause the antiforgery cookie to get the `Secure` flag when actual transport is HTTP, causing subsequent antiforgery validation to fail (the cookie would not be sent back over HTTP). This is self-defeating, not exploitable.

**Consolidated impact of forwarded header spoofing:**

| Header | Affected code | Spoofing impact | Severity |
|--------|--------------|-----------------|----------|
| `X-Forwarded-For` | `Login.cshtml.cs:39` rate limiter | Bypass 5-attempt lockout (requires direct container access) | Negligible |
| `X-Forwarded-Proto` | `OAuthCallback.cshtml.cs:79` callback URL | Redirect URI mismatch breaks OAuth (self-inflicted DoS) | None |
| `X-Forwarded-Proto` | Antiforgery cookie (no explicit SecurePolicy) | Could set/unset `Secure` flag incorrectly | Negligible |
| `X-Forwarded-Proto` | Auth cookie (line 53), session cookie (line 64) | No impact (`CookieSecurePolicy.Always`) | None |

**Final recommendation: Close as won't-fix.** The `Clear()` pattern is correct for Docker per Microsoft documentation. Both stated attack vectors are either factually wrong or require direct container access (covered by issue #0061). The proposed remediation of hardcoding `KnownProxies` is impractical for Docker deployments.
