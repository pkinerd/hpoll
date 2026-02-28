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
