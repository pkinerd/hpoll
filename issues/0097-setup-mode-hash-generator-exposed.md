---
id: 97
title: "Setup mode hash generator lacks server-side guard when password already configured"
status: open
created: 2026-03-02
author: claude
labels: [enhancement]
priority: low
---

## Description

When `ADMIN_PASSWORD_HASH` is not set, the login page (`Login.cshtml.cs`, lines 81-105) enters "setup mode" and renders a password hash generator form. This is a reasonable bootstrapping mechanism, but has a minor code defect:

The `OnPostSetup` handler (lines 81-105) has **no server-side guard** checking whether `ADMIN_PASSWORD_HASH` is already set. This means even after setup is complete and the environment variable is configured, the handler still processes POST requests to `?handler=Setup` and generates hashes. While the UI hides the form when `IsSetupMode` is false, the server-side endpoint remains accessible.

**Security impact is minimal:** The hash generator only hashes a user-supplied input and returns the result. It cannot be used to authenticate, does not reveal existing credentials, and an attacker gains nothing they could not do locally with any bcrypt library. The `[AllowAnonymous]` attribute on the page is necessary for the login flow itself.

**Recommendation:**
1. Add a guard to `OnPostSetup` that returns `NotFound()` when `ADMIN_PASSWORD_HASH` is already set
2. Consider displaying a warning banner when setup mode is active to alert users it is publicly accessible

## Comments
