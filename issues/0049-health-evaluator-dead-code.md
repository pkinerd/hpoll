---
id: 49
title: "HealthEvaluator class is dead code — never registered or used"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [code-quality]
priority: low
---

## Description

**Severity: Low**

`Hpoll.Core/Services/HealthEvaluator.cs` defines a `HealthEvaluator` class with `IsHubHealthy()` and `NeedsAttention()` methods, but:
1. It is not registered in the DI container of either `Worker/Program.cs` or `Admin/Program.cs`
2. It is never referenced by any other code in the solution
3. It has no interface defined

The class appears to be intended for hub health monitoring but was never integrated.

**Options:**
1. **Remove** it if hub health monitoring is not planned
2. **Integrate** it into the polling or email workflows (e.g., include health status in daily summary emails, or skip polling for unhealthy hubs)
3. **Add an interface** (`IHealthEvaluator`) and register it if it will be used in the future

## Comments

### claude — 2026-03-01

**Comprehensive review update:** In addition to being dead code, `HealthEvaluator` also breaks the project's architectural convention by lacking a corresponding interface. All other services follow the `IFoo`/`Foo` pattern (`IHueApiClient`/`HueApiClient`, `IEmailRenderer`/`EmailRenderer`, `IEmailSender`/`SesEmailSender`), but `HealthEvaluator` is a concrete class with no `IHealthEvaluator` interface.

Additionally, the class uses a different `using` directive style (inside the namespace block) compared to the rest of the codebase (file-scoped namespaces with `using` at top).

When resolving this issue, either: (1) integrate it into the polling and dashboard code paths with a proper `IHealthEvaluator` interface, or (2) remove it entirely if it's not needed.
