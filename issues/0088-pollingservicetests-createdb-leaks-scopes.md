---
id: 88
title: "PollingServiceTests.CreateDb() leaks service scopes"
status: closed
closed: 2026-03-02
created: 2026-03-02
author: claude
labels: [testing, code-quality]
priority: low
---

## Description

The `CreateDb()` helper method in `PollingServiceTests` (line 51-55 in `tests/Hpoll.Worker.Tests/PollingServiceTests.cs`) creates a new `IServiceScope` but never disposes it:

```csharp
private HpollDbContext CreateDb()
{
    var scope = _serviceProvider.CreateScope();
    return scope.ServiceProvider.GetRequiredService<HpollDbContext>();
}
```

Each call creates a scope that stays alive until `_serviceProvider` is disposed. Since xUnit creates a fresh test class instance per test method, the leaked scopes live for the duration of one test only — making the actual resource leak negligible. However, disposing the `DbContext` via `using var db = CreateDb()` does **not** dispose the owning scope, so the scope reference is lost.

The same pattern exists in two other test files:
- `EmailSchedulerServiceTests.cs` (lines 45-48)
- `TokenRefreshServiceTests.cs` (lines 42-46)

Any fix should be applied consistently across all three files.

**Recommendation:** Return the scope alongside the context and use `using` at call sites:
```csharp
private (IServiceScope Scope, HpollDbContext Db) CreateDb()
{
    var scope = _serviceProvider.CreateScope();
    return (scope, scope.ServiceProvider.GetRequiredService<HpollDbContext>());
}
// Usage: using var (scope, db) = CreateDb();
```

**Location:** `tests/Hpoll.Worker.Tests/PollingServiceTests.cs:51-55`

## Comments

### claude — 2026-03-02

Implemented: fixed scope leaks in CreateDb() helpers across 4 test files using scope tracking list

