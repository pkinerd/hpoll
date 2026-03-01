---
id: 81
title: "Login rate-limiting lockout path is untested (security-critical)"
status: open
created: 2026-03-01
author: claude
labels: [testing, security]
priority: high
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

### claude — 2026-03-01

**Critical review of the issue's reasoning — several flaws identified:**

**1. Priority contradicts severity assessment (fixed above: medium → high).**
The title and description both call this "security-critical" and a "key security
control" protecting against brute-force attacks. Yet the priority was `medium`.
A security-critical untested code path warrants `high` at minimum. Updated.

**2. Recommendation #2 is impractical without code refactoring.**
"Verify lockout expires after the duration window" is stated as if it were a
straightforward test to write. It is not. `DateTime.UtcNow` is hardcoded at
line 44 and `LockoutDuration` is a `private static readonly TimeSpan` at
line 18. There is no `TimeProvider` or `ISystemClock` injection. To test expiry
you would either need to: (a) wait 15 real minutes — impractical; (b) use
reflection to manipulate the static `_failedAttempts` dictionary; or (c)
refactor the code to inject a time abstraction (e.g., .NET 8's `TimeProvider`).
The issue should acknowledge this is a **code change**, not just a new test.

**3. Static shared state creates test isolation problems (unmentioned).**
`_failedAttempts` is a `private static readonly ConcurrentDictionary` (line 15).
Since it's static, entries persist across test instances in the same process.
The recommended test (#1) — submitting 5+ failures from the same IP — would
leave residual state that leaks into subsequent tests. The existing tests
already have this latent problem: they all default to IP `127.0.0.1`, and the
`OnPostAsync_WrongPassword_ReturnsError` test silently increments the failure
counter in the shared dictionary. Each test should use a unique IP, and ideally
the rate-limiter should be injectable/resettable.

**4. Overlooks the "unknown" IP coalescing risk — a bigger security concern.**
Line 39: `HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"`.
When `RemoteIpAddress` is null (common behind certain proxies or in test
environments), all such requests share the single dictionary key `"unknown"`.
Five failed attempts from *any combination* of unidentifiable clients locks out
*every* unidentifiable client. This is an accidental denial-of-service vector
that is arguably a more significant security concern than the missing test
coverage. The issue does not mention it.

**5. Overlooks unbounded memory growth — resource exhaustion vector.**
The `_failedAttempts` dictionary is never cleaned up proactively. The only
cleanup paths are: (a) a locked-out IP returns after expiry (line 49), and
(b) successful login (line 64). If an attacker generates failures from thousands
of distinct IPs without ever triggering cleanup, the dictionary grows
indefinitely. There is no background sweep or TTL eviction. For code described
as "security-critical," this resource exhaustion vector is a notable omission.

**6. Coverage numbers are unverifiable assertions.**
The issue states "Line coverage is 89.6% / branch coverage is 65.4% (17/26)"
as fact but provides no link to a coverage report or CI artifact. These specific
numbers cannot be independently verified.

**7. The `returnUrl` redirect (line 75) is unrelated to rate limiting.**
The issue title is about "rate-limiting lockout" but recommendation #3 (test
the `ReturnUrl` redirect) has nothing to do with rate limiting. It is a
separate uncovered branch. Grouping it under a rate-limiting issue conflates
two distinct concerns and makes the scope unclear.

**Revised recommendations:**
1. **Refactor for testability first**: inject `TimeProvider` (available in
   .NET 8) and make the rate-limiter resettable or injectable, so expiry and
   isolation can be properly tested.
2. Then write the lockout tests: 5+ failures → lockout message, expiry →
   access restored, unique IPs per test to avoid static state leakage.
3. Split the `ReturnUrl` redirect coverage into a separate issue — it is an
   independent uncovered branch, not a rate-limiting concern.
4. Consider opening separate issues for the "unknown" IP coalescing risk and
   the unbounded memory growth, as these are live security/reliability defects
   beyond just missing tests.
