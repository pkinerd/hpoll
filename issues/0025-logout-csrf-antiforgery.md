---
id: 25
title: "Logout endpoint lacks antiforgery validation (CSRF logout)"
status: closed
closed: 2026-03-03
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

### claude (critical review round 2) — 2026-03-01

**Assessment: PARTIALLY_VALID -- the issue is technically correct and the previous reviews' SameSite=Lax mitigation argument has a subtle gap, but the impact remains firmly low/informational.**

**Detailed findings:**

1. **The issue's core claim is accurate.** The logout endpoint at `src/Hpoll.Admin/Program.cs` line 93 (`MapPost("/Logout", ...)`) does not call `.RequireAntiforgery()`. Minimal API endpoints do NOT auto-validate antiforgery tokens (unlike Razor Pages POST handlers). The layout form at `src/Hpoll.Admin/Pages/Shared/_Layout.cshtml` line 64 uses raw HTML `<form method="post" action="/Logout">` rather than tag-helper attributes (`asp-page`, `asp-controller`), so no `__RequestVerificationToken` hidden field is injected. Both the generation and validation sides are missing.

2. **The endpoint is POST-only** (line 93: `app.MapPost`), which is correct practice. A GET-based logout would be far worse since browsers, prefetchers, and crawlers could trigger it.

3. **Antiforgery is configured globally** at `src/Hpoll.Admin/Program.cs` lines 68-71 (`builder.Services.AddAntiforgery(...)`), and Razor Pages are added at line 73 (`builder.Services.AddRazorPages()`). Razor Pages auto-validate antiforgery on POST handlers by default. However, the logout endpoint is a Minimal API endpoint, not a Razor Page, so this global Razor Pages behavior does not apply to it. There is no `[ValidateAntiForgeryToken]` attribute (that's an MVC concept) and no `[IgnoreAntiforgeryToken]` attribute either.

4. **Critical nuance the previous reviews missed regarding SameSite=Lax.** The prior reviews argue that `SameSite=Lax` (line 52) blocks cross-site POST requests from including the auth cookie, making CSRF logout unexploitable. This analysis is incomplete because the endpoint is marked `.AllowAnonymous()` (line 97). Here is the actual attack flow:
   - An attacker's page auto-submits a form: `<form method="post" action="https://target/Logout">`.
   - The browser sends the POST without the auth cookie (SameSite=Lax correctly blocks it on cross-origin POSTs).
   - The endpoint executes anyway because it is `AllowAnonymous` -- no authentication is required.
   - `SignOutAsync` (line 95) adds a `Set-Cookie` response header that expires/deletes the auth cookie.
   - The browser processes this response as a top-level navigation to the target origin, so the `Set-Cookie` header is in a first-party context and IS processed.
   - Result: the admin's auth cookie is deleted and they are logged out.

   Therefore, **SameSite=Lax does NOT fully mitigate this CSRF logout** for this specific `AllowAnonymous` endpoint. The attack works because the endpoint does not need the cookie to be present in the request -- it only needs to execute and return a cookie-clearing response. The previous review's claim that this is "not an exploitable vulnerability" is technically incorrect.

5. **Impact assessment remains low/informational despite the exploitability.** Even though the CSRF logout attack is technically viable:
   - The worst-case impact is the admin must re-enter their password. No data is exposed, no privilege is escalated, no state is corrupted.
   - OWASP and most security frameworks classify CSRF logout as informational or low severity.
   - This is an internal admin portal for a Hue sensor monitoring service, not a high-value target.
   - The attack requires the victim to visit a malicious page while authenticated, and the only result is a minor inconvenience.
   - The issue's description labels this "Severity: Medium" which overstates the real-world risk.

6. **The description's severity label is contradictory.** The frontmatter says `priority: low` but the body says "Severity: Medium". These should be reconciled -- `low` is appropriate for both.

7. **The suggested remediation is reasonable but low-priority.** Adding `.RequireAntiforgery()` to the endpoint and switching the form to use `@Html.AntiForgeryToken()` (or converting to a Razor Page) would be a clean fix and good practice, but should not be prioritized over functional work.

**Verdict:** The issue is technically valid and the CSRF logout is actually exploitable (contrary to the previous review's conclusion), but the impact is firmly low/informational. The "Medium" severity label in the description overstates the risk. Recommend keeping as `low` priority with a `defense-in-depth` label. This is a legitimate but low-value improvement.

### claude — 2026-03-03

Fixed: Added IAntiforgery.IsRequestValidAsync validation to the MapPost('/Logout') endpoint and @Html.AntiForgeryToken() to the layout's logout form. POST without a valid token now returns 400 Bad Request. Added 4 integration tests covering: POST without token (400), POST with valid token (redirect to /Login), form contains antiforgery token, and GET still rejected.
