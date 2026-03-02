---
id: 90
title: "Integration tests share database state creating fragile tests"
status: closed
closed: 2026-03-02
created: 2026-03-02
author: claude
labels: [testing, code-quality]
priority: medium
---

## Description

Integration test classes in `tests/Hpoll.Admin.Tests/Integration/` use `IClassFixture<HpollWebApplicationFactory>`. xUnit's `IClassFixture<T>` creates one fixture instance **per test class** (not shared across classes), so each test class gets its own independent SQLite in-memory database. However, tests **within the same class** share a single database and do not clean up between test methods, leading to:

1. **Order-dependent tests:** Later tests within a class see data from earlier tests. For example, `CustomersIndex_ShowsNoCustomersMessage_WhenEmpty` has to explicitly call `db.Customers.RemoveRange(db.Customers)` to clear residual data. Similarly, `About_ShowsNoSystemInfoMessage_WhenWorkerNotStarted` calls `db.SystemInfo.RemoveRange(db.SystemInfo)`.
2. **Weak assertions:** `Dashboard_ShowsNoPollActivityMessage_WhenEmpty` only checks for the section header rather than asserting no entries, because `Dashboard_ShowsRecentPollingActivity` (an earlier test in the same class) seeds a polling log that persists.

Additionally, `EmailRendererTests` uses `UseInMemoryDatabase` (EF Core in-memory provider) while all other test files use `UseSqlite` with an in-memory SQLite connection. The EF Core in-memory provider does not enforce foreign key constraints and behaves differently from SQLite, creating a consistency gap. (Note: each `EmailRendererTests` instance gets a fresh `Guid`-named database, so isolation between tests there is fine — the concern is only the FK behavior difference.)

**Recommendation:**
- Add a `ResetDatabase()` method on `HpollWebApplicationFactory` that clears all tables between tests
- Or use `IAsyncLifetime` with per-test database seeding/cleanup
- Migrate `EmailRendererTests` to `UseSqlite` for consistency

**Location:** `tests/Hpoll.Admin.Tests/Integration/HpollWebApplicationFactory.cs`

## Comments

### claude — 2026-03-02

Implemented: added ResetDatabaseAsync() to HpollWebApplicationFactory with IAsyncLifetime in 4 integration test classes

