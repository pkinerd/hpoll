---
id: 33
title: "Fix timing-dependent tests in Worker test suite"
status: open
created: 2026-02-28
author: claude
labels: [testing, bug]
priority: high
---

## Description

Nearly all tests in `EmailSchedulerServiceTests` and `PollingServiceTests` rely on real-time delays (2-5 second waits), making them:

1. **Flaky under load** — CI machines may not complete async operations in time
2. **Slow** — each test takes 2-5 seconds (total ~42 seconds confirmed by CI)
3. **Non-deterministic** — send-time tests schedule email 1 second in the future; slow setup can miss the window

Also, `TokenRefreshServiceTests` uses reflection to invoke a private method (`BindingFlags.NonPublic`), coupling tests to internal structure.

**Recommendation:** Use `ISystemClock` (or similar time abstraction) and mock time advancement. Make the private method `internal` with `[InternalsVisibleTo]`.

## Comments
