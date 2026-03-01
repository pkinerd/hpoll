---
id: 48
title: "Persist Data Protection keys and add Secure flag to session cookie"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: low
---

## Description

**Severity: Medium**

Two related session security issues:

**1. Data Protection keys not persisted** (`Program.cs`): ASP.NET Core Data Protection is not explicitly configured. By default, encryption keys for cookies are stored in a transient location. When the Docker container restarts, all existing authentication cookies become invalid, forcing all admins to re-login. This also means the session cookie encryption changes on every restart.

**2. Session cookie missing Secure flag** (`Program.cs` lines 59-64): The session cookie options do not set `SecurePolicy` or `SameSite`:
```csharp
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // Missing: options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // Missing: options.Cookie.SameSite = SameSiteMode.Lax;
});
```

The session cookie stores OAuth CSRF tokens for the hub registration flow. Without the Secure flag, it could be sent over insecure connections.

**Remediation:**
1. Configure Data Protection key persistence to the shared data volume: `builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/app/data/keys"));`
2. Add `options.Cookie.SecurePolicy = CookieSecurePolicy.Always;` and `options.Cookie.SameSite = SameSiteMode.Lax;` to session options.

**Related:** #26 (auth cookie SecurePolicy)

## Comments

### claude — 2026-03-01

**Consolidated from #0026 and #0063 (closed as subsets of this issue).**

This issue now encompasses the full HTTPS/cookie hardening task for the admin portal:

3. **Auth cookie SecurePolicy** (from #0026): In `Admin/Program.cs` line 53, `CookieSecurePolicy.SameAsRequest` means the auth cookie can be transmitted over HTTP. Change to `CookieSecurePolicy.Always` for production. The default docker-compose setup exposes port 8080 without TLS.

4. **HTTPS redirect middleware** (from #0063): Add `app.UseHttpsRedirection()` and `app.UseHsts()` conditionally in production. The `ForwardedHeaders` middleware is already configured for proxy setups. Document that a reverse proxy with TLS termination is required for production deployment.

All four items (Data Protection keys, session cookie Secure flag, auth cookie SecurePolicy, HTTPS redirect) should be implemented together in a single PR since they all involve `Program.cs` and form a coherent "production HTTPS hardening" change.

### claude — 2026-03-01

**Items 2 and 3 addressed** in commit `5742eb0` on branch `claude/email-notification-not-unique-GsP8B`:

- Auth cookie: changed `CookieSecurePolicy.SameAsRequest` → `CookieSecurePolicy.Always` (item 3 / #26)
- Session cookie: added `SecurePolicy = CookieSecurePolicy.Always` and `SameSite = SameSiteMode.Lax` (item 2)

Remaining open items on this issue:
1. Data Protection key persistence (`PersistKeysToFileSystem`)
4. HTTPS redirect middleware (`UseHttpsRedirection` / `UseHsts`)

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. **Items 2 and 3 are FIXED** (commit 5742eb0): auth cookie and session cookie both have SecurePolicy.Always. Remaining: Data Protection key persistence is a UX issue (session loss on restart), not a security vulnerability. HTTPS redirect is architecturally questionable for HTTP-only container behind reverse proxy. Note: antiforgery cookie still missing SecurePolicy.

### critical-review — 2026-03-01

**Verdict: PARTIALLY_VALID. Recommend keeping priority at low.**

Detailed source review of `src/Hpoll.Admin/Program.cs` on `main` against each claim:

**Item 2 (session cookie Secure flag) -- ALREADY FIXED.** The current code at lines 59-66 reads:
```csharp
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
```
Both `SecurePolicy` and `SameSite` are set. The issue description's code snippet is stale -- it reflects a pre-fix state. This item is fully resolved and should be removed from the open scope.

**Item 3 (auth cookie SecurePolicy) -- ALREADY FIXED.** Line 53 now reads `options.Cookie.SecurePolicy = CookieSecurePolicy.Always;`. The earlier comment claiming it was `SameAsRequest` is no longer accurate for `main`. Also resolved.

**Item 1 (Data Protection key persistence) -- VALID but low severity.** There is no call to `AddDataProtection()` or `PersistKeysToFileSystem()` anywhere in the codebase. ASP.NET Core defaults to ephemeral in-memory keys inside Docker containers (the runtime does not find a user profile directory). On container restart, all existing authentication and session cookies are invalidated. However, the practical impact is limited:

- **Authentication cookies:** The admin logs in with a password. Losing the cookie on restart forces a re-login, which is a minor UX inconvenience, not a security vulnerability.
- **Session data:** Sessions use `AddDistributedMemoryCache()` (in-memory), so session state is already lost on restart regardless of whether Data Protection keys persist. The session stores only short-lived OAuth CSRF tokens (`OAuthCsrf`, `OAuthCustomerId`) for the Hue hub registration flow. If the container restarts mid-OAuth-flow, the CSRF token is lost and the callback will fail with "CSRF validation failed" -- but the user can simply retry. This is a narrow, low-probability failure mode.
- **Conclusion:** Persisting keys to `/app/data/keys` is a reasonable improvement for operational stability but is not a security fix. The `/app/data` volume is already mounted in `docker-compose.yml`, so the fix is trivial. Severity should remain low.

**Item 4 (HTTPS redirect middleware) -- NOT RECOMMENDED.** The container listens on plain HTTP (port 8080) and is designed to sit behind a reverse proxy that terminates TLS. Adding `UseHttpsRedirection()` inside the container would either (a) do nothing because `ForwardedHeaders` already rewrites the scheme based on `X-Forwarded-Proto`, or (b) cause redirect loops if misconfigured. The `ForwardedHeaders` middleware is correctly configured at lines 15-20 and line 84. HSTS headers should be set at the reverse proxy, not in the application. This item should be closed as won't-fix.

**Additional observation:** The antiforgery cookie configuration at lines 68-71 only sets `SameSite` and does not set `SecurePolicy`. This means the antiforgery token cookie could be sent over HTTP. While antiforgery tokens are not session credentials, adding `SecurePolicy = CookieSecurePolicy.Always` would be consistent with the hardening applied to the auth and session cookies.

**Summary of remaining actionable items:**
1. **(Low)** Add Data Protection key persistence to file system for operational stability.
2. **(Low)** Add `SecurePolicy = CookieSecurePolicy.Always` to the antiforgery cookie for consistency.
3. **Close item 4** (HTTPS redirect) as architecturally inappropriate for this deployment model.
