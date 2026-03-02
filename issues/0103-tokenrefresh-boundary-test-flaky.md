---
id: 103
title: "TokenRefreshService boundary test has overly weak assertion"
status: closed
closed: 2026-03-02
created: 2026-03-02
author: claude
labels: [testing, code-quality]
priority: low
---

## Description

The test `RefreshExpiringTokens_TokenExpiresAtBoundary_RefreshesToken` in `TokenRefreshServiceTests.cs` (line 381) uses `Times.AtMostOnce()` instead of `Times.Once()`.

**This is NOT a flaky/nondeterministic test** as initially reported. Because the service uses `>` (strict greater-than) comparison at line 101 (`if (timeUntilExpiry > refreshThreshold) continue;`), and time always advances between seed and check, `timeUntilExpiry` will always be strictly less than the 48-hour threshold. The token will **always** be refreshed.

The actual problem is a **weak assertion**: `Times.AtMostOnce()` allows zero calls, which means the test would pass even if the service had a bug that prevented refreshing boundary-case tokens. The test name promises it verifies that a refresh occurs (`RefreshesToken`), but the assertion doesn't enforce that.

**Location:** `tests/Hpoll.Worker.Tests/TokenRefreshServiceTests.cs`, line 381

**Recommendation:**
Change `Times.AtMostOnce()` to `Times.Once()` on line 381. The service already accepts an optional `TimeProvider` parameter — optionally inject a fake `TimeProvider` to make the boundary condition fully explicit.

## Comments

### claude — 2026-03-02

Implemented: changed Times.AtMostOnce() to Times.Once() in boundary test

