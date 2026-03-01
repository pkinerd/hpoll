---
id: 25
title: "Logout endpoint lacks antiforgery validation (CSRF logout)"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: low
---

## Description

**Severity: Medium**

The Logout endpoint in `Admin/Program.cs` lines 91-95 is a Minimal API `MapPost` endpoint that does not perform antiforgery validation. The form in `_Layout.cshtml` uses a raw `action="/Logout"` without generating an antiforgery token. A malicious page could POST to `/Logout` to force-logout the admin (CSRF logout attack).

**Remediation:** Add `.RequireAntiforgery()` to the Logout endpoint, and use `@Html.AntiForgeryToken()` in the form, or switch to a Razor Page which handles this automatically.

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. The auth cookie already has SameSite=Lax (Program.cs line 52), which prevents cross-site POST requests in all modern browsers -- the exact attack vector described. CSRF logout is classified as low severity by OWASP. Adding antiforgery would be defense-in-depth, not fixing an exploitable vulnerability.
