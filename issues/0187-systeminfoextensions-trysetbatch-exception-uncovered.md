---
id: 187
title: "SystemInfoServiceExtensions.TrySetBatchAsync exception paths uncovered"
status: open
created: 2026-03-16
author: claude
labels: [testing]
priority: low
---

## Description

The `TrySetBatchAsync` extension method in `SystemInfoServiceExtensions` has the `OperationCanceledException` re-throw path (line 20) uncovered by tests. The generic exception catch with warning log (line 24) IS covered by existing tests (`TrySetBatchAsync_SwallowsExceptions` and `TrySetBatchAsync_LogsWarningOnException`).

**Location:** `src/Hpoll.Core/Interfaces/SystemInfoServiceExtensions.cs`, line 20

**Impact:** Low — this is a single catch-and-rethrow line for `OperationCanceledException` when the cancellation token is triggered. The behavior is important for graceful shutdown but the code is trivial.

**Found by:** Code coverage analysis (finding 6)

**Recommendation:** Add one unit test where `ISystemInfoService.SetBatchAsync` throws `OperationCanceledException` with a cancelled token — verify it propagates rather than being swallowed.

## Comments
