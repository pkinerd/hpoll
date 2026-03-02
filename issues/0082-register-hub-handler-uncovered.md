---
id: 82
title: "OnPostRegisterHubAsync OAuth handler entirely uncovered by tests"
status: closed
closed: 2026-03-02
created: 2026-03-01
author: claude
labels: [testing, security]
priority: medium
---

## Description

The `OnPostRegisterHubAsync` handler in `Customers/Detail.cshtml.cs` (lines 222-249) has 0%
test coverage. This handler:

- Generates a CSRF token and stores it in the session
- Constructs the Hue OAuth authorization URL with client_id, state parameter, and callback URL
- Redirects the user to the external Hue OAuth endpoint

This is security-sensitive code involving session state management and CSRF token generation
for OAuth flows. The entire block including session manipulation and URL construction is
untested.

**Found by:** Comprehensive review — code coverage analysis.

**Recommendation:** Add unit tests that mock `ISession` and `IOptions<HueAppSettings>` to
verify:
1. A CSRF token is stored in session state
2. The OAuth URL is correctly constructed with client_id, state, and callback URL
3. The response redirects to the Hue OAuth endpoint

## Comments

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

The core finding -- that `OnPostRegisterHubAsync` has zero test coverage -- is accurate and
worth addressing. However, several claims in the issue contain inaccuracies.

**Claim-by-claim analysis:**

1. **"lines 222-249"** -- Slightly off. The method signature `public async Task<IActionResult>
   OnPostRegisterHubAsync(int id)` begins at line 220 of `Detail.cshtml.cs`. The body spans
   lines 221-249. The issue states "lines 222-249" which omits the declaration itself. Minor
   inaccuracy.

2. **"Generates a CSRF token and stores it in the session"** -- Accurate. Line 237 generates a
   GUID-based token via `Guid.NewGuid().ToString("N")`, and line 238 stores it with
   `HttpContext.Session.SetString("OAuthCsrf", csrfToken)`. Line 239 also stores the customer
   ID in session via `HttpContext.Session.SetInt32("OAuthCustomerId", id)`, which the issue
   omits.

3. **"Constructs the Hue OAuth authorization URL"** -- Accurate. Lines 242-246 build the URL
   with `client_id`, `response_type=code`, `state`, and `redirect_uri` parameters.

4. **"Redirects the user to the external Hue OAuth endpoint"** -- **Inaccurate.** The handler
   does NOT redirect. It sets the `OAuthUrl` property (a public `string?` property declared at
   line 53) and returns `Page()` at line 248. The Razor page then presumably renders this URL
   as a link or button for the user to click. This is an important distinction: the handler
   returns a page result, not a redirect result. This error is repeated in recommendation
   item 3 ("The response redirects to the Hue OAuth endpoint"), which is also wrong.

5. **"0% test coverage"** -- Accurate. A search for `OnPostRegisterHub` across the entire
   codebase finds it only in the source file itself. The test file at
   `tests/Hpoll.Admin.Tests/Customers/DetailModelTests.cs` covers `OnGetAsync`,
   `OnPostUpdateNameAsync`, `OnPostToggleStatusAsync`, `OnPostUpdateEmailsAsync`,
   `OnPostUpdateSendTimesAsync`, and `OnPostUpdateTimeZoneAsync` -- but has no test for
   `OnPostRegisterHubAsync`.

6. **"security-sensitive code involving session state management and CSRF token generation"** --
   Accurate characterization. The OAuth state parameter is a security control against CSRF
   attacks, and the session storage of both the CSRF token and customer ID is security-relevant.

**On the recommendation:**

- Mocking `ISession` is correct -- the existing `CreatePageModel()` helper uses
  `DefaultHttpContext` which does not have session services enabled, so tests would need to
  set up a mock or in-memory session provider.
- `IOptions<HueAppSettings>` is already mocked in the test helper via
  `Options.Create(new HueAppSettings())`, but tests would need to populate `ClientId` and
  `CallbackUrl` to get past the guard clause at lines 230-234. The recommendation should
  specify this.
- Recommendation item 3 should say "the response returns a PageResult with the OAuthUrl
  property populated" rather than "redirects to the Hue OAuth endpoint."

**On priority and labels:**

Medium priority is appropriate. The `security` label is justified given the OAuth CSRF token
handling. The `testing` label is appropriate.

**Summary:** The core issue is real and actionable. The handler is indeed untested. However,
the claim that the handler "redirects" is wrong -- it renders a page with the OAuth URL. The
line numbers are slightly off (should be 220-249, not 222-249). The recommendation should be
corrected to reflect the actual behavior (PageResult, not redirect).

### claude (critical review, second pass) — 2026-03-01

**Verdict: PARTIALLY_VALID**

Concurring with the prior review. The 0% coverage claim is confirmed and the redirect
inaccuracy is a clear factual error. Adding a few supplementary observations:

1. **Risk is mitigated by callback-side tests.** The issue frames this as high-risk
   security-sensitive code, but the corresponding OAuth callback handler in
   `OAuthCallbackModel` is well-tested (see `OAuthCallbackModelTests.cs` with 14+ test
   methods). Those tests cover CSRF token validation, missing session state, mismatched
   state parameters, and token exchange. If `OnPostRegisterHubAsync` produced a malformed
   state value or failed to store the CSRF token, the callback tests would fail to validate
   it. The generation side has lower standalone risk because bugs would manifest as callback
   failures, not silent security bypasses.

2. **The issue omits the config guard clause.** Lines 230-234 check whether `ClientId` and
   `CallbackUrl` are configured, returning a page with an error message if not. This is a
   meaningful code path (approximately 20% of the handler logic) that the issue description
   does not mention at all. Tests should cover both the happy path (config present) and the
   error path (config missing).

3. **The issue omits the `OAuthCustomerId` session storage.** Line 239 stores
   `HttpContext.Session.SetInt32("OAuthCustomerId", id)` alongside the CSRF token. This is
   not mentioned in the issue description or the recommended test assertions. This value is
   used by the callback handler to associate the OAuth response with the correct customer.

4. **`TestSession` already exists and can be reused.** The `OAuthCallbackModelTests.cs` file
   contains a private `TestSession` class (lines 340-363) implementing `ISession` with an
   in-memory dictionary. Rather than creating new session mocking, this class should be
   extracted to a shared test utility and reused. This makes the testing effort lower than
   the issue implies.

5. **CSRF token is GUID-based, not cryptographically random.** The handler uses
   `Guid.NewGuid().ToString("N")` for the CSRF token. While sufficient for OAuth state
   parameters (since the token is also stored server-side in session), this is worth noting
   for any future security hardening review. GUIDs are not guaranteed to be
   cryptographically unpredictable on all platforms.

6. **Priority.** Medium is acceptable, though the actual risk profile (given callback-side
   test coverage) leans toward low. The testing gap is real but the security exposure is
   limited.

**Corrected recommendation:** Add unit tests that:
1. Verify a non-empty CSRF token is stored in session under key `"OAuthCsrf"`
2. Verify the customer ID is stored under key `"OAuthCustomerId"`
3. Verify the `OAuthUrl` property contains the correct `client_id`, `state` (format:
   `{customerId}:{csrfToken}`), `response_type=code`, and `redirect_uri` query parameters
4. Verify the handler returns `PageResult` (not a redirect)
5. Verify the error path when `HueAppSettings.ClientId` or `CallbackUrl` is empty
6. Verify `NotFoundResult` when the customer ID does not exist

Extract `TestSession` from `OAuthCallbackModelTests.cs` to a shared helper to enable
session testing in `DetailModelTests`.

### claude — 2026-03-02

Comprehensive review (code coverage analysis) found additional detail:
Coverage analysis confirms `OnPostRegisterHubAsync` (lines 222-249) is entirely untested — 0% coverage on this handler. This includes:
- Validation of `_hueApp.ClientId`/`CallbackUrl` configuration
- CSRF token generation and session state management
- OAuth URL construction with state parameter
The handler handles security-sensitive OAuth flow setup and should be prioritized for test coverage.

### claude — 2026-03-02

Implemented: added 4 OnPostRegisterHubAsync tests in DetailModelTests.cs

