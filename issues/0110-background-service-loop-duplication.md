---
id: 110
title: "SystemInfo metric update pattern duplicated across 3 background services"
status: closed
closed: 2026-03-03
created: 2026-03-02
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

The "try to update system info metrics, log warning on failure" sub-pattern is duplicated across three background services.

**Locations:**
- `src/Hpoll.Worker/Services/PollingService.cs` (inside `ExecuteAsync` loop)
- `src/Hpoll.Worker/Services/TokenRefreshService.cs` (inside `ExecuteAsync` loop)
- `src/Hpoll.Worker/Services/DatabaseBackupService.cs` (inside `RunBackupCycleAsync`)

**Duplicated sub-pattern:**
```csharp
try
{
    await _systemInfo.SetAsync("category", "key", value);
    // more SetAsync calls
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to update system info metrics");
}
```

**Note:** The outer `ExecuteAsync` loop structures differ meaningfully between services — `DatabaseBackupService` delays first then works, `EmailSchedulerService` uses dynamic sleep duration and has no inline `_systemInfo` calls in its loop. A shared base class would add more complexity than it removes. Only the `_systemInfo` try/catch sub-pattern is truly duplicated.

**Recommendation:**
Extract a small helper method — either a `TrySetAsync` extension on `ISystemInfoService` that swallows exceptions internally, or a local static helper — to remove the 15-20 duplicated try/catch lines across the three services.

## Comments

### claude — 2026-03-03

Fixed: Created `TrySetBatchAsync` extension method on `ISystemInfoService` in `SystemInfoServiceExtensions.cs`. Refactored all 3 services (PollingService, TokenRefreshService, DatabaseBackupService) to use it, eliminating the duplicated try/catch pattern. Added 4 unit tests for the extension method.
