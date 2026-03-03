---
id: 129
title: "SystemInfoService uses DateTime.UtcNow instead of TimeProvider"
status: open
created: 2026-03-03
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

`SystemInfoService` at `src/Hpoll.Worker/Services/SystemInfoService.cs` uses `DateTime.UtcNow`
directly for `UpdatedAt` timestamps (lines 35, 42, 64, 73). All other background services in the
Worker project (`PollingService`, `TokenRefreshService`, `EmailSchedulerService`,
`DatabaseBackupService`) use the injected `TimeProvider` for time abstraction.

For consistency with all other Worker services, `SystemInfoService` should also use `TimeProvider`.
The practical impact is minimal since the timestamps are purely informational metadata (displayed
on the admin About page), and the service is already thoroughly tested with 9 unit tests. This is
a codebase consistency concern rather than a testability blocker.

**Recommendation:** Inject `TimeProvider` into `SystemInfoService` and use
`_timeProvider.GetUtcNow().UtcDateTime` instead of `DateTime.UtcNow`, consistent with
other Worker services.

**Related:** #0076 (closed — addressed Admin pages and entities, but missed this service)

**Found by:** Comprehensive review — code quality review.

## Comments
