---
id: 163
title: "README incorrectly states password hashing uses PBKDF2-SHA256 (actual: SHA512)"
status: open
created: 2026-03-15
author: claude
labels: [documentation]
priority: medium
---

## Description

The README states at line 347: "The password is hashed using PBKDF2-SHA256 with a random salt."

However, the code uses `Microsoft.AspNetCore.Identity.PasswordHasher<object>`, which in .NET 8 defaults to the Identity V3 format: PBKDF2 with HMAC-SHA512 at 100,000 iterations (not SHA256). The V3 format existed since .NET Core 3.0 but originally used HMAC-SHA256; the switch to HMAC-SHA512 happened in .NET 7.0.

**Location:** `README.md` line 347

**Recommendation:** Change to: "The password is hashed using ASP.NET Core Identity's PasswordHasher." to avoid specifying algorithm details that could change with framework updates. Alternatively: "The password is hashed using ASP.NET Core Identity's PasswordHasher (PBKDF2 with HMAC-SHA512, 100,000 iterations)."
