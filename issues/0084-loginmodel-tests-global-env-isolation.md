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
