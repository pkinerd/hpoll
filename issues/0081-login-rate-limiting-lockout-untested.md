---
id: 81
title: "Login rate-limiting lockout path is untested (security-critical)"
status: open
created: 2026-03-01
author: claude
labels: [testing, security]
priority: medium
---

## Description

The login rate-limiting lockout mechanism at `Login.cshtml.cs` lines 42-49 is security-critical
but has 0% coverage:

- The lockout check (when a client IP exceeds `MaxAttempts` = 5) at lines 44-47
- The "Too many failed attempts" error return
- The lockout expiry cleanup at line 49
- The `returnUrl` redirect on successful login at line 75

This code protects against brute-force password attacks and is a key security control.
Line coverage for Login.cshtml.cs is 89.6% but branch coverage is only 65.4% (17/26).

**Found by:** Comprehensive review — code coverage analysis and unit testing review.

**Recommendation:** Add tests that:
1. Submit 5+ failed login attempts from the same IP and verify lockout message
2. Verify lockout expires after the duration window
3. Test successful login with a `ReturnUrl` query parameter

## Comments

### claude (critical review) — 2026-03-01

**Verdict: VALID**

Every claim in this issue checks out against the actual source code. Detailed verification below.

**Line number verification:**
- Lines 42-49 in `src/Hpoll.Admin/Pages/Login.cshtml.cs` do contain the rate-limiting lockout mechanism. Specifically: line 42 is the `TryGetValue` + `Count >= MaxAttempts` check, line 44 is the `DateTime.UtcNow < record.ResetAt` inner check, line 46 sets the "Too many failed attempts" error message, line 47 returns `Page()`, and line 49 calls `TryRemove` to clear the expired lockout record. All accurate.
- `MaxAttempts = 5` is declared at line 17. Correct.
- The `returnUrl` redirect is at line 75 (`return LocalRedirect(returnUrl);`). Correct.

**Coverage claim verification:**
- The unit test file `tests/Hpoll.Admin.Tests/LoginModelTests.cs` contains 8 tests. For `OnPostAsync`, there are tests for: no password hash configured, correct password (default redirect), and wrong password (single attempt). None of these tests submit more than one failed login attempt, so the lockout path (lines 42-50) is never exercised. The claim of 0% coverage for the lockout path is accurate.
- The `OnPostAsync_CorrectPassword_RedirectsToIndex` test verifies the redirect to `/Index` but never sets a `ReturnUrl` query parameter, so the `LocalRedirect(returnUrl)` path at lines 72-76 is also untested. Correct.
- The integration tests in `tests/Hpoll.Admin.Tests/Integration/LoginPageTests.cs` are all GET requests against the login page and do not exercise `OnPostAsync` at all.
- The specific coverage numbers (89.6% line, 65.4% branch, 17/26) cannot be independently verified without running the coverage tool, but they are plausible given that the lockout branch, the lockout-expiry branch, the ReturnUrl branch, and the null IP fallback branch are all untested, which accounts for a meaningful gap in branch coverage.

**Severity/priority assessment:**
- The "medium" priority is appropriate. The rate-limiting code is security-critical (brute-force protection), and having zero test coverage for it means regressions could silently break this protection. However, the code itself is straightforward and correct upon inspection, so the risk is about future regressions rather than a present bug.
- The "security" and "testing" labels are both appropriate.

**Additional observation:**
- The `_failedAttempts` dictionary is `static`, which means writing tests for the lockout path requires care to avoid test pollution between parallel test runs. The existing `CreatePageModel` helper uses `"127.0.0.1"` as the default IP; lockout tests should use unique IP addresses per test or run sequentially to avoid interference. The issue's recommendations are sound but should note this implementation detail.

**Grouping concern:**
- The `returnUrl` redirect (bullet 4, recommendation 3) is logically separate from rate-limiting -- it is an untested branch, but not a security-critical one in the same way as brute-force protection. Grouping it here is acceptable for convenience but slightly conflates two different concerns.

**Conclusion:** All claims are accurate and well-referenced. The recommendations are appropriate. No corrections needed to the issue description.

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

The core finding -- that the lockout code path and returnUrl redirect are untested -- is accurate and well-identified. However, the issue overstates the security significance and conflates unrelated concerns. Detailed analysis follows.

**Line numbers and factual claims are accurate.** Lines 42-49 of `Login.cshtml.cs` contain the rate-limiting lockout logic exactly as described. `MaxAttempts = 5` at line 17, `LockoutDuration = TimeSpan.FromMinutes(15)` at line 18, the lockout check at line 42, the "Too many failed attempts" error at line 46, the expiry cleanup at line 49, and the returnUrl redirect at line 75 all match. No corrections needed to the referenced line numbers.

**The "0% coverage" claim for the lockout path is substantiated by test inspection.** `LoginModelTests.cs` contains 8 `[Fact]` tests. The three `OnPostAsync` tests each submit exactly one login attempt: one with no hash configured, one correct password, one wrong password. None exercise the lockout threshold. No test references `"Too many"`, `MaxAttempts`, or submits repeated failed attempts. The integration tests in `LoginPageTests.cs` are all GET requests. The claim of zero coverage on the lockout path is well-supported.

**The "security-critical" characterization is overstated.** The rate-limiting implementation uses a `private static readonly ConcurrentDictionary`, which has significant limitations:

1. **No persistence** -- Application restarts clear all lockout state. An attacker who can trigger a restart (or simply wait for a deployment) bypasses it entirely.
2. **Trivially bypassed via IP rotation** -- Proxies, VPNs, or cloud infrastructure make IP-based rate limiting ineffective against determined attackers.
3. **Unbounded memory growth** -- Failed attempts from distinct IPs accumulate without eviction. Only the specific locked-out IP's record is cleaned up, and only when that same IP makes another attempt after expiry (line 49). There is no background cleanup or size cap.
4. **Not shared across instances** -- In any multi-instance deployment, each process has its own dictionary, effectively multiplying the allowed attempts.

Calling this a "key security control" implies it is load-bearing for authentication security. In reality, it provides marginal defense-in-depth against unsophisticated brute-force attempts from a single IP. The admin portal already uses password hashing via `PasswordHasher<object>` (which uses PBKDF2 with a high iteration count), which is the actual key security control. Testing the rate limiter is still worthwhile -- broken security code is worse than absent security code -- but the framing should reflect its limited effectiveness.

**The returnUrl redirect is a separate concern, not rate limiting.** Bullet 4 ("the `returnUrl` redirect on successful login at line 75") and recommendation 3 ("test successful login with a `ReturnUrl` query parameter") are unrelated to rate limiting. The returnUrl redirect is an authentication flow feature. Bundling it into a "rate-limiting lockout" issue conflates two distinct testing gaps. Additionally, the returnUrl code at lines 72-76 includes a `Url.IsLocalUrl(returnUrl)` check, which is itself a security-relevant validation (open redirect prevention) that deserves its own focused attention rather than being a footnote in a rate-limiting issue.

**Testability concern is underemphasized.** The issue's recommendations are mechanically correct but omit an important implementation detail. Because `_failedAttempts` is `private static readonly`, its state persists across all test instances within a single test run. The existing `CreatePageModel` helper does accept an `ipAddress` parameter (defaulting to `"127.0.0.1"`), so lockout tests could use unique IP addresses per test. However, recommendation 2 ("verify lockout expires after the duration window") is particularly challenging because `LockoutDuration` is a `private static readonly TimeSpan` -- there is no way to inject a shorter duration for testing. Tests would either need to wait 15 real minutes, use reflection to modify the field, or the production code would need to be refactored to accept the duration as a dependency. This practical barrier to implementation should have been noted.

**Coverage percentages are unverifiable but plausible.** The specific numbers (89.6% line, 65.4% branch, 17/26) cannot be confirmed without running the coverage tooling. However, counting the untested branches -- the outer lockout check (line 42, true/false), the inner expiry check (line 44, true/false), the returnUrl presence check (line 73), and the `IsLocalUrl` check (line 73) -- yields roughly 4-5 uncovered branches out of a plausible total, which is consistent with the claimed 65.4%.

**Priority assessment.** Medium priority is reasonable for the testing gap itself. However, if the issue is truly about security, the more impactful follow-up would be to refactor the rate-limiting mechanism to address its architectural limitations (persistence, memory bounds, instance sharing) rather than merely adding tests for the existing flawed implementation.

**Summary:** The factual claims about untested code paths are accurate and verified. The issue is weakened by (a) overstating the security criticality of an ephemeral in-memory IP-based rate limiter, (b) bundling the unrelated returnUrl redirect concern, and (c) not addressing the practical testability challenges posed by static private fields with fixed durations. The issue would be more precise if titled "Login lockout and returnUrl branches untested" without the "security-critical" qualifier, or if the rate-limiting and returnUrl gaps were filed as separate issues.

### claude — 2026-03-02

Comprehensive review (code coverage analysis) found additional detail:
Coverage analysis confirms: Login.cshtml.cs has 89.6% line coverage but only 65.4% branch coverage (17/26 branches). The specific uncovered paths are:
1. Rate limiting lockout path (lines 44, 46-47, 49): branch where `DateTime.UtcNow < record.ResetAt` is true
2. Lockout expiry path: where the lockout has expired and the record is removed
3. Return URL redirect (line 75): valid local URL `LocalRedirect` path
4. Null IP address fallback (line 39): `?? "unknown"` branch
The documentation review also notes the brute-force protection mechanism is entirely undocumented (no class-level summary, constants not configurable).
