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

### claude (critical review) — 2026-03-01

**Assessment: LOW_VALUE -- technically accurate observation but practically mitigated and low-impact.**

**Findings from codebase examination:**

1. **The logout endpoint** is defined at `src/Hpoll.Admin/Program.cs` lines 92-97 as a Minimal API `MapPost("/Logout")`. The comment on line 92 even says "POST to prevent CSRF-triggered logouts," showing the developer was already thinking about this. The endpoint calls `SignOutAsync` and redirects to `/Login`. It is marked `.AllowAnonymous()`.

2. **The form in the layout** at `src/Hpoll.Admin/Pages/Shared/_Layout.cshtml` line 64 uses a raw HTML `<form method="post" action="/Logout">` rather than a Razor tag-helper form. Because it uses raw `action="/Logout"` instead of `asp-page` or `asp-controller`/`asp-action`, the ASP.NET Core tag helper that normally auto-injects the `__RequestVerificationToken` hidden field does not activate. The `_ViewImports.cshtml` does include `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`, but tag helpers only fire on elements that use `asp-*` attributes. So the issue is correct that no antiforgery token is sent with the logout POST.

3. **The endpoint lacks antiforgery validation.** The `MapPost` endpoint at line 93 does not chain `.RequireAntiforgery()`. Minimal API endpoints do not auto-validate antiforgery tokens (unlike Razor Pages which auto-validate on POST handlers by default). So the issue is technically correct: no token is generated, no token is sent, and no token is validated.

4. **However, SameSite=Lax provides strong mitigation.** The authentication cookie is configured with `SameSite = SameSiteMode.Lax` at `src/Hpoll.Admin/Program.cs` line 52, and `SecurePolicy = CookieSecurePolicy.Always` at line 53. With `SameSite=Lax`, the browser will not attach the authentication cookie on cross-site POST requests. This means a malicious external page submitting a form to `/Logout` would not include the auth cookie, making the user appear unauthenticated already. The `SignOutAsync` call would effectively be a no-op (clearing an already-absent cookie). This blocks the classic CSRF logout vector in all modern browsers.

5. **The antiforgery cookie is also SameSite=Lax** (`src/Hpoll.Admin/Program.cs` lines 68-71), so even if an antiforgery token were required, the antiforgery cookie itself would not be sent cross-site either, producing a double layer of protection.

6. **Real-world risk assessment for this specific application:**
   - This is an internal admin portal for a Hue monitoring service, not a consumer-facing application.
   - A CSRF logout attack is a denial-of-convenience, not a data breach or privilege escalation. The worst case is the admin is forced to re-enter their password.
   - OWASP classifies CSRF logout as informational/low severity.
   - The `SameSite=Lax` cookie attribute already blocks the cross-origin POST attack vector in all browsers that support it (all modern browsers since ~2020).
   - The only scenario where this could be exploited is from a same-site origin (a different application on the same registrable domain), which is not a realistic threat model for this application.

7. **The description has a minor inaccuracy.** The issue says lines 91-95, but the actual endpoint spans lines 92-97 (line 92 is the comment, lines 93-97 are the code including `.AllowAnonymous()`). This is trivial but noted for completeness.

**Verdict:** The observation is technically correct -- the logout endpoint does lack antiforgery validation and the form does not include a token. However, this is **not an exploitable vulnerability** given the existing `SameSite=Lax` cookie configuration. The severity label says "Medium" in the description while the frontmatter says "low" priority, which is contradictory. The actual severity is informational at best. Adding antiforgery would be a marginal defense-in-depth improvement, but this issue should not be prioritized over functional work. Recommend status change to `wontfix` or keeping as `low` with a "nice-to-have" label rather than `security`.
