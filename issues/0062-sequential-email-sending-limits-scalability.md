---
id: 62
title: "Sequential email sending limits scalability for large customer counts"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, performance]
priority: low
---

## Description

In `EmailSchedulerService.SendAllEmailsAsync` (lines 120-139), emails are sent to each customer sequentially in a `foreach` loop. Each iteration involves rendering the HTML email body (DB queries) and sending via AWS SES (network round trip). For a service with many customers, this serializes all network latency.

```csharp
foreach (var customer in customers)
{
    var html = await _emailRenderer.RenderDailySummaryAsync(...);
    await _emailSender.SendEmailAsync(...);
}
```

**File:** `src/Hpoll.Worker/Services/EmailSchedulerService.cs:120-139`

**Recommended fix:** For small customer counts this is fine. As the customer base grows, consider:
1. `Parallel.ForEachAsync` with controlled concurrency (requires separate `DbContext` scopes per task)
2. A semaphore-limited `Task.WhenAll` pattern
3. SES rate limit awareness (SES has per-second sending limits)

Note: The `DbContext` is not thread-safe, so each parallel task would need its own scope via `IServiceScopeFactory`.

**Source:** Efficiency review finding E9

## Comments
