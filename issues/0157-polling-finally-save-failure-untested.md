---
id: 157
title: "PollingService finally-block SaveChangesAsync failure path untested"
status: open
created: 2026-03-15
author: claude
labels: [testing]
priority: low
---

## Description

In `src/Hpoll.Worker/Services/PollingService.cs` (lines 289-301), the `finally` block of `PollHubAsync` calls `SaveChangesAsync(CancellationToken.None)` to persist the hub's `LastPolledAt` and `LastSuccessAt` fields even during shutdown. If this save fails (e.g., database full, locked, or corrupted), the exception is caught and logged.

This error handling path has no test coverage. The catch block is straightforward defensive code — it logs and swallows the exception. While there is a test for cleanup exceptions (`CleanupOldData_ExceptionDuringCleanup_LogsWarning`), no test verifies that a `SaveChangesAsync` failure in the `finally` block is caught and logged without crashing.

Note: The finally block does not control whether the next hub is polled — that's handled by the calling loop, which is already tested by multi-hub tests.

**Related:** This finding was originally identified in issue #36 (closed) as one of the target scenarios but was explicitly deferred as a "lower-priority edge case."

**Recommended test:** Simulate a database failure during the finally-block save (e.g., by disposing the DbContext or configuring the mock to throw), then verify the service logs a warning and does not propagate the exception.

**Source:** Comprehensive review (unit testing review), 2026-03-15

## Comments
