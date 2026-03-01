---
id: 48
title: "Persist Data Protection keys and add Secure flag to session cookie"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: medium
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
