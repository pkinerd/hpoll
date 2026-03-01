---
id: 77
title: "OAuthCallback Razor view has 0% test coverage"
status: open
created: 2026-03-01
author: claude
labels: [testing]
priority: low
---

## Description

The OAuthCallback Razor view (`src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml`) has 0% line
coverage (0/12 lines) and 0% branch coverage (0/24 branches). The view renders success/failure
states for the OAuth callback with conditional display of device counts, hub links, and customer
navigation.

The code-behind (`OAuthCallback.cshtml.cs`) is at 100% coverage with 14 unit tests covering
all business logic, security checks (CSRF validation, OAuth flow), and error handling. The Razor
template itself is never exercised by any test, but it contains only presentational logic.

Uncovered conditional branches include:
- Success vs failure state rendering (line 9)
- Device count display (line 13)
- Hub navigation link (line 20)
- Customer navigation link (line 24)
- Error-state customer link (line 33)

**Found by:** Comprehensive review — code coverage analysis.

**Recommendation:** Consider low-priority. See critical review comment below for analysis of
why the original recommendation (integration tests) is impractical and the priority was
overstated.

## Comments

### claude — 2026-03-01

**Critical review — flaws in original issue reasoning:**

**1. The view is NOT "security-relevant" — priority downgraded to low.**
The original issue calls this "a security-relevant page that handles OAuth callback results."
This is misleading. The `.cshtml` template handles nothing — it is a purely presentational
template that renders HTML based on model properties already set by the code-behind. All
security-critical logic (CSRF token validation, OAuth authorization code exchange, token
storage, error handling) lives entirely in `OAuthCallback.cshtml.cs`, which has 100% coverage
across 14 unit tests. The view just displays success/failure messages and navigation links.
There is no security risk from untested HTML rendering. Priority changed from `high` to `low`.

**2. The "0/24 branches" count is inflated and misleading.**
The `.cshtml` file has only 5 `@if` conditionals at the source level (10 source-level branches).
The "24 branches" figure comes from IL-level instrumentation of the Razor-generated C# code,
which includes compiler-generated branches for `Nullable<T>.HasValue` property access, null
checks on tag helper attributes, `WriteLiteral`/`Write` call chains, and other boilerplate
from the Razor source generator. Presenting this as "24 branches" of uncovered logic
dramatically overstates the actual gap.

**3. The integration test recommendation is impractical with the current test infrastructure.**
The OAuthCallback endpoint requires server-side session state (`OAuthCsrf` and
`OAuthCustomerId`) to be populated before the callback request for the success path. The
existing `HpollWebApplicationFactory` has no session manipulation support — none of the 20+
existing integration tests use sessions. Testing the success path would require:
- First hitting the customer page's "Register Hub" POST action to populate session state
- Capturing and replaying the session cookie on the callback request
- Mocking the `IHueApiClient` responses (which IS supported via `MockHueApiClient`)
This multi-request flow with session coordination is significantly more complex than all
existing integration tests, which are simple GET-and-assert patterns.

**4. Razor view coverage is inherently low-value with coverlet.**
Coverlet instruments the generated C# code from Razor compilation, not the template itself.
The instrumented code consists of `WriteLiteral()`, `Write()`, `BeginWriteAttribute()`, and
similar framework calls. Achieving "coverage" for these lines verifies that the Razor engine
emitted HTML strings — not that the template logic is correct. This is fundamentally different
from covering business logic in C# classes.

**5. What would actually be valuable (if anything).**
If template correctness is a concern, the most practical approach would be a handful of unit
tests that render the page model with pre-set properties (Success=true with HubId, Success=false
with CustomerId, etc.) and assert on the HTML output. However, given that the template is 41
lines of trivial conditional rendering with no logic beyond `@if (Model.Property)`, even this
is of marginal value.
