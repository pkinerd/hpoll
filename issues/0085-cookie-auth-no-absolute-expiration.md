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

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

**Line numbers and code references:**
The issue references `Program.cs` lines 45-54 for the cookie authentication configuration. This is accurate. Lines 45-54 contain exactly the `AddAuthentication`/`AddCookie` block:
```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
```
Line numbers are correct.

**Claim: No explicit `SlidingExpiration` setting (defaults to `true`):**
Verified. A grep for `SlidingExpiration` across the entire codebase returns zero results. The ASP.NET Core default for `CookieAuthenticationOptions.SlidingExpiration` is indeed `true`. This claim is accurate.

**Claim: Active sessions never expire:**
This is an overstatement. With `SlidingExpiration = true` and `ExpireTimeSpan = 24 hours`, the cookie **does** expire if the user is inactive for 24 hours. What the issue actually describes is that there is no *absolute* expiration -- a session that is actively used will keep renewing its 24-hour window indefinitely. However, "active sessions never expire" conflates "no absolute timeout" with "never expires." The session does expire after 24 hours of inactivity. For a more precise characterization: an actively-used session has no upper bound on its lifetime.

Additionally, the `SignInAsync` call at `Login.cshtml.cs` line 70 passes no `AuthenticationProperties`, which means `IsPersistent` defaults to `false`. With `IsPersistent = false`, the cookie is a session cookie (no `Expires` header), meaning it is deleted when the browser is closed, regardless of `ExpireTimeSpan`. The `ExpireTimeSpan` still governs server-side ticket validation, but the browser-level cookie does not survive browser restarts. This is an important mitigating factor not mentioned in the issue.

**Claim: No session invalidation when admin password hash changes:**
Verified. The login flow in `Login.cshtml.cs` creates claims with only `ClaimTypes.Name = "admin"` (line 66). There is no password-hash-derived stamp or version token in the claims. The cookie validation pipeline has no event handler that checks whether the password hash has changed. This claim is accurate.

**Claim: No way to force logout all sessions:**
Partially accurate. There is no explicit "invalidate all sessions" mechanism. However, since the app uses the default in-memory data protection keys (no persisted key ring is configured), restarting the application container would invalidate all existing cookies because the encryption keys are regenerated. In a Docker deployment, this is a practical (if crude) mechanism. That said, it is true there is no intentional administrative control for this.

**Assessment of severity/priority:**
The "low" priority rating is appropriate, and arguably even generous for the deployment context. This is a single-user admin portal behind Docker, likely on an internal network, with session cookies (non-persistent) that are destroyed on browser close. The practical attack scenario -- an attacker who has stolen a session cookie and keeps it alive through continuous use -- is quite narrow given that: (1) the cookie has `HttpOnly`, `Secure`, and `SameSite=Lax` protections, (2) the cookie does not survive browser restarts, and (3) the portal is single-tenant. The OWASP reference (A07:2021) is technically applicable but the real-world risk is minimal.

**Recommendation assessment:**
Setting `SlidingExpiration = false` would mean sessions hard-expire after 24 hours, which is reasonable but changes UX (an admin actively working gets logged out). A better approach might be to keep sliding expiration but add a custom claim with an "issued at" timestamp, then validate it in `OnValidatePrincipal` to enforce a maximum absolute lifetime (e.g., 72 hours). The suggestion to incorporate a token version in claims for password-change invalidation is sound in principle but adds complexity that may not be justified for a single-user admin portal.

**Summary:** The core finding -- that sliding expiration without an absolute timeout allows indefinitely renewable sessions -- is technically correct. However, the issue overstates the practical impact by not acknowledging that (1) `IsPersistent` is false, making these session cookies that die on browser close, and (2) the deployment context is a single-user admin portal. The sub-findings about session invalidation are accurate but low-impact given the architecture. Priority "low" is appropriate.

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

**Line number and code reference verification:**

- The issue references `Program.cs` lines 45-54. The actual cookie authentication
  configuration spans lines 45-54, which is correct. The `ExpireTimeSpan = TimeSpan.FromHours(24)`
  is at line 50, confirmed. The claim that there is no explicit `SlidingExpiration` setting is
  also correct — a search of the entire codebase returns zero matches for `SlidingExpiration`.

- The claim that ASP.NET Core defaults `SlidingExpiration` to `true` is correct. Per Microsoft
  documentation, `CookieAuthenticationOptions.SlidingExpiration` defaults to `true`.

**Assessment of the core finding:**

The technical observation is accurate: with `SlidingExpiration` defaulting to `true` and
`ExpireTimeSpan` set to 24 hours, the cookie lifetime is renewed on every request that
occurs after the halfway point (12 hours). This means an actively-used session will never
reach its expiration, effectively creating an indefinite session for a continuously active
user.

However, "indefinite" requires nuance. The session only renews when a request arrives after
50% of the `ExpireTimeSpan` has elapsed (the 12-hour mark). A user would need to make at
least one request every 24 hours to keep the session alive. If the user is inactive for a
full 24-hour window, the session expires. This is a sliding window, not truly "never expires."

**Assessment of the sub-issues:**

1. "No absolute session timeout — active sessions never expire" — Technically accurate given
   the sliding behavior, but overstated. The session does expire if there is a 24-hour gap
   in activity. For a single-user admin portal that is likely accessed intermittently (not
   continuously), this is less of a practical concern than it sounds.

2. "No session invalidation mechanism when the admin password hash changes" — This is a valid
   observation. The `SignInAsync` call at `Login.cshtml.cs:70` creates a `ClaimsPrincipal`
   with only a `ClaimTypes.Name = "admin"` claim and no password-hash-derived token. Changing
   `ADMIN_PASSWORD_HASH` would not invalidate existing cookies. However, since the cookie
   encryption keys are stored in the default ASP.NET Core Data Protection location (in-memory
   or in the container filesystem), restarting the Docker container to apply the new env var
   would also rotate the data protection keys, effectively invalidating all existing sessions
   anyway. This only matters if the password hash is changed without a container restart, which
   is not a typical deployment pattern.

3. "No way to force logout all sessions" — Accurate, but standard for simple cookie auth in
   ASP.NET Core. Restarting the application (container) achieves this effect as a side effect
   of data protection key rotation for in-memory key stores.

**Priority assessment:**

The issue is labeled `low` priority, which is reasonable — arguably even generous. Consider
the deployment context:

- This is a **single-user** admin portal (the only user is "admin").
- It runs in a **Docker container**, typically behind a reverse proxy on an internal network.
- The cookie is configured with `HttpOnly`, `SameSite=Lax`, and `SecurePolicy=Always` (lines
  51-53), which are strong protections against cookie theft.
- The practical attack scenario requiring an absolute session timeout (stolen session token
  that remains valid indefinitely) is significantly mitigated by these cookie security flags.

The OWASP reference to A07:2021 is appropriate but the severity in this context is very low.

**Recommendation assessment:**

The recommendation to set `SlidingExpiration = false` is the simplest fix and would create
a hard 24-hour absolute timeout. This is a reasonable one-line change. The recommendation
to add a token version in claims is over-engineered for a single-user admin portal where
container restarts already invalidate sessions.

**Summary:** The core technical finding (sliding expiration creates indefinitely-renewable
sessions) is accurate and the code references are correct. The practical severity is overstated
given the single-user deployment context, strong cookie security flags, and the fact that
container restarts already rotate data protection keys. The `low` priority rating is appropriate.
The simplest fix (adding `SlidingExpiration = false`) would be a trivial one-line change if
desired.

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

Prior reviews have covered the core facts thoroughly. This review focuses on additional
angles not yet addressed.

**Factual accuracy of the issue:**

All technical claims are confirmed accurate by code inspection:
- `ExpireTimeSpan = TimeSpan.FromHours(24)` at `Program.cs` line 50: confirmed.
- `SlidingExpiration` not explicitly set anywhere (zero matches in codebase): confirmed.
  ASP.NET Core defaults it to `true`.
- No password-hash-derived claim or `OnValidatePrincipal` handler: confirmed. The claims
  at `Login.cshtml.cs` line 66 contain only `ClaimTypes.Name = "admin"`.

**New observation: the "renewed on each request" claim is imprecise.**

The issue states the cookie is "automatically renewed on each request." This is incorrect.
ASP.NET Core's sliding expiration only reissues the cookie when more than 50% of the
`ExpireTimeSpan` has elapsed. With a 24-hour window, renewal only occurs on requests made
after the 12-hour mark. This is a meaningful distinction -- the renewal mechanism is not
triggered on every request, which slightly limits the renewal surface.

**New observation: `IsPersistent = false` is a significant unmentioned mitigating factor.**

Neither the issue nor its sub-points acknowledge that `SignInAsync` at `Login.cshtml.cs`
line 70 is called without `AuthenticationProperties { IsPersistent = true }`. This means
the browser treats the cookie as a session cookie (no `Expires`/`Max-Age` header). The
cookie is destroyed when the user closes their browser. In practice, the "indefinite
session" scenario requires the user to never close their browser for the duration. The
server-side `ExpireTimeSpan` ticket validation still applies, but the browser-level session
boundary provides an additional natural expiration point that the issue fails to consider.

**Is OWASP A07:2021 appropriate?**

A07:2021 covers a broad category including default credentials, broken session management,
and missing MFA. The sliding expiration behavior does fall under "session management" within
this category. However, applying an OWASP Top 10 reference without contextualizing severity
is misleading. The OWASP reference would be more appropriate if this were a multi-user
internet-facing application. For a single-user Docker-deployed admin portal with strong
cookie flags (`HttpOnly`, `Secure`, `SameSite=Lax`), citing A07:2021 without qualification
creates a false sense of urgency.

**Is the recommendation practical?**

The issue recommends `SlidingExpiration = false` **or** adding an absolute session timeout.
These are not equivalent:

1. `SlidingExpiration = false` with the current 24h `ExpireTimeSpan` creates a hard cutoff
   at 24 hours from login. This is simple but has a UX cost: an admin actively working at
   hour 23 gets logged out at hour 24 without warning. For a monitoring portal where an
   admin might be troubleshooting an issue over several hours, this could be disruptive.

2. Adding an absolute timeout via `AuthenticationProperties.ExpiresUtc` at sign-in while
   keeping `SlidingExpiration = true` would be more user-friendly. For example, setting
   `ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)` as an absolute cap while keeping the
   24h sliding window would allow natural usage patterns while bounding total session life.

The issue conflates these two approaches with "or" as though they are interchangeable, but
they have very different UX and security characteristics.

The token-version-on-password-change recommendation is technically sound but impractical
here: `ADMIN_PASSWORD_HASH` is an environment variable, so changing it requires a container
restart, which already invalidates all sessions by rotating in-memory data protection keys.
The recommendation solves a problem that does not exist in the actual deployment model.

**Priority assessment:**

Low priority is correct. Given the mitigating factors (session cookie behavior, strong
cookie security flags, single-user context, container restart invalidation), this is a
defense-in-depth improvement with negligible real-world impact. If prioritization is needed,
this should be addressed only after all medium and higher issues are resolved.

**Summary:** The issue's technical observations are accurate but the framing omits the
critical `IsPersistent = false` mitigating factor and overstates practical risk. The
"renewed on each request" phrasing is imprecise (renewal only occurs after the halfway
point). The OWASP reference, while technically applicable, is contextually disproportionate.
The recommendations conflate two approaches with different trade-offs. The issue is valid
as a low-priority hardening note but should not be treated as a meaningful security gap.
