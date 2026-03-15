---
id: 166
title: "EmailSchedulerService uses two SetAsync calls instead of SetBatchAsync"
status: closed
closed: 2026-03-15
created: 2026-03-15
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

In `EmailSchedulerService`, after sending an email, two separate `SetAsync` calls update runtime metrics (lines 139-140):

```csharp
await _systemInfo.SetAsync("Runtime", "runtime.last_email_sent", metricTime.ToString("O"));
await _systemInfo.SetAsync("Runtime", "runtime.total_emails_sent", _totalEmailsSent.ToString());
```

All other background services (`PollingService`, `TokenRefreshService`, `DatabaseBackupService`) use `TrySetBatchAsync` for this purpose, which writes multiple metrics in a single database operation. The `EmailSchedulerService` is the only service that issues separate calls.

A third `SetAsync` call at line 165 (`runtime.next_email_due`) runs at a different point in the flow and is fine as a standalone call.

**Location:** `src/Hpoll.Worker/Services/EmailSchedulerService.cs`, lines 139-140

**Category:** quality, consistency

**Severity:** low — functionally correct but inconsistent with established patterns and performs unnecessary extra database operations.

**Recommendation:** Replace lines 139-140 with a single `TrySetBatchAsync` call:

```csharp
await _systemInfo.TrySetBatchAsync("Runtime", new Dictionary<string, string>
{
    ["runtime.last_email_sent"] = metricTime.ToString("O"),
    ["runtime.total_emails_sent"] = _totalEmailsSent.ToString()
}, ct);
```

This halves the database operations and aligns with how other services update their metrics.

## Comments

### claude — 2026-03-15

Fixed: Replaced two separate SetAsync calls with a single TrySetBatchAsync call for runtime metrics. Also converted the standalone next_email_due SetAsync to use TrySetBatchAsync. Updated corresponding tests.
