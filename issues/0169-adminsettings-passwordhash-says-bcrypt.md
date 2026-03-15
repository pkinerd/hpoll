---
id: 169
title: "AdminSettings.PasswordHash XML doc incorrectly says BCrypt"
status: closed
closed: 2026-03-15
created: 2026-03-15
author: claude
labels: [documentation]
priority: low
---

## Description

The `AdminSettings.PasswordHash` property has an XML doc comment that says:

```csharp
/// <summary>BCrypt hash of the admin password. Null or empty triggers first-time setup mode.</summary>
```

The implementation does not use BCrypt. It uses ASP.NET Core Identity's `PasswordHasher<object>`, which implements PBKDF2 (see `Login.cshtml.cs`). This is the same class of documentation bug as #163 (README said SHA256 when it was SHA512), and is a follow-up to #152 whose fix inadvertently preserved the BCrypt inaccuracy.

**Location:** `src/Hpoll.Core/Configuration/CustomerConfig.cs`, line 96

**Category:** misleading-docs

**Severity:** low — a one-word documentation fix on an internal code comment. Developers integrating with this code would see the `PasswordHasher<object>` usage directly in `Login.cshtml.cs`.

**Recommendation:** Update the XML doc comment to avoid specifying the exact algorithm (which may change with .NET version updates):

```csharp
/// <summary>ASP.NET Core Identity PasswordHasher hash of the admin password. Null or empty triggers first-time setup mode.</summary>
```

## Comments

### claude — 2026-03-15

Fixed: Updated XML doc comment to say "ASP.NET Core Identity PasswordHasher hash" instead of "BCrypt hash".
