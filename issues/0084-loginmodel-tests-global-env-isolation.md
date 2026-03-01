---
id: 84
title: "LoginModel tests use global Environment creating test isolation risk"
status: open
created: 2026-03-01
author: claude
labels: [testing, code-quality]
priority: medium
---

## Description

`LoginModelTests` uses `Environment.SetEnvironmentVariable("ADMIN_PASSWORD_HASH", ...)` to
configure test state. While each test sets the variable and `Dispose()` cleans it up, this
creates shared mutable global state.

Risks:
- If tests run in parallel (xUnit default for different test classes), two test classes reading
  this environment variable could see unexpected values
- The window between `SetEnvironmentVariable` and `Dispose` can overlap with other classes
- Any test that reads `ADMIN_PASSWORD_HASH` during a `LoginModelTests` run could be affected

**Found by:** Comprehensive review — unit testing review.

**Recommendation:** Refactor `LoginModel` to accept the password hash as an injected dependency
(e.g., through `IOptions<AdminSettings>`) rather than reading `Environment.GetEnvironmentVariable`
directly. This would allow tests to use clean dependency injection without mutating global state.

## Comments

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

The issue correctly identifies the use of `Environment.SetEnvironmentVariable` as shared mutable
global state. However, it overstates the practical risk and understates what would actually be
required to fix this properly.

**What the issue gets right:**

The core observation is accurate: `LoginModelTests` calls `Environment.SetEnvironmentVariable`
in 5 of its 9 tests (`OnGet_NoPasswordHash_SetsSetupMode`, `OnGet_WithPasswordHash_NotSetupMode`,
`OnPostAsync_NoPasswordHashConfigured_ReturnsSetupMode`, `OnPostAsync_CorrectPassword_RedirectsToIndex`,
`OnPostAsync_WrongPassword_ReturnsError`), and the `Dispose()` method clears it back to null
after each test. This is indeed global mutable state.

**Where the issue overstates the risk:**

1. **xUnit parallelism within a class is sequential.** xUnit v2 runs tests *within* a single
   test class sequentially by default. So the 5 tests inside `LoginModelTests` will never
   interfere with each other. The issue's framing ("test isolation risk") could mislead readers
   into thinking intra-class conflicts are possible -- they are not.

2. **Cross-class interference is real but narrow.** The only place where `ADMIN_PASSWORD_HASH`
   is read outside `LoginModelTests` is in `LoginPageTests` (an integration test using
   `WebApplicationFactory`). The `LoginPageTests.Login_ShowsEitherSignInOrSetup` test
   acknowledges it could see either state by asserting `hasSignIn || hasSetup`, so it is already
   written to tolerate any value of the environment variable. None of the other integration tests
   (`DashboardPageTests`, `AboutPageTests`, `CustomersPageTests`, `HubsPageTests`,
   `LayoutAndNavigationTests`) read `ADMIN_PASSWORD_HASH`. No other test classes in the Worker
   or Core test assemblies reference it either (those run in separate processes anyway).

3. **The practical blast radius is essentially zero today.** Since the only potentially affected
   test (`Login_ShowsEitherSignInOrSetup`) already guards against both states, there is no
   currently observable test flakiness from this pattern.

**Assessment of the recommendation:**

The suggestion to refactor `LoginModel` to use `IOptions<AdminSettings>` is architecturally
sound as a general principle, but the issue frames it purely as a testing concern when it would
actually be a production code change to `Login.cshtml.cs`. The `LoginModel` page model currently
calls `Environment.GetEnvironmentVariable("ADMIN_PASSWORD_HASH")` directly in both `OnGet()`
and `OnPostAsync()`. Introducing DI would require:
- Creating a new `AdminSettings` options class
- Registering it in `Program.cs` with environment variable binding
- Adding a constructor parameter to `LoginModel` (which currently has no constructor)
- Updating all tests

This is a reasonable improvement for maintainability and testability, but it is not urgent given
that the actual risk of test interference is effectively mitigated by the existing integration
test design.

**Priority assessment:** Medium is too high for a theoretical concern with zero practical impact
today. Low would be more appropriate. This is a "nice to have" code quality improvement, not a
defect or reliability risk that needs prioritization.

**Suggested re-characterization:** This issue would be more accurately titled as a production
code improvement ("LoginModel should use DI for configuration instead of direct env var access")
rather than a test isolation concern. The testing benefit would be a side effect of a cleaner
production architecture.
