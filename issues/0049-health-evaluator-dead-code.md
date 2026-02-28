---
id: 49
title: "HealthEvaluator class is dead code — never registered or used"
status: open
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
