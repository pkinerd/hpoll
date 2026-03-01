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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Recommend wontfix. This is an SQLite single-writer home monitoring service with single-digit customer counts. Code snippet inaccurate (loop at line 122, not 120). Per-customer send times (d97a36a) stagger delivery naturally. Parallel.ForEachAsync with SQLite would just contend on the database lock. Even 50 customers would take ~15 seconds, well within the 1-minute scheduler tolerance.

### critical-review — 2026-03-01

**Verdict: INVALID — recommend closing as wontfix.**

This issue contains multiple factual inaccuracies and proposes an optimization that would add
meaningful complexity with no practical benefit at the scale this service operates.

**Factual errors in the issue:**

1. **Method name is wrong.** The issue references `EmailSchedulerService.SendAllEmailsAsync` — this
   method does not exist. The actual method is `ProcessDueCustomersAsync` (line 105).
2. **Line numbers are wrong.** The issue cites lines 120-139. The `foreach` loop begins at line 122,
   and the method body extends to approximately line 167.
3. **Code snippet is misleading.** The issue shows a simplified inline `_emailRenderer` / `_emailSender`
   loop. The actual code calls `SendCustomerEmailAsync`, updates per-customer metrics via
   `_systemInfo.SetAsync`, tracks `_totalEmailsSent`, and advances `NextSendTimeUtc` — none of which
   appear in the snippet. This misrepresents the complexity involved in parallelizing the loop.

**Why parallelization is inappropriate here:**

1. **Scale does not warrant it.** This is a Philips Hue home/office monitoring service. Each customer
   represents a physical premises with Hue Bridge hardware. Realistic customer counts are single-digit
   to low double-digit. The issue title claims "large customer counts" without evidence that such scale
   is expected or even possible for this product.

2. **Per-customer send times already stagger delivery.** Customers have individual `SendTimesLocal` and
   `TimeZoneId` fields. `ProcessDueCustomersAsync` only processes customers whose `NextSendTimeUtc <= now`.
   Customers in different timezones (or with different configured send times) naturally become due at
   different times, so the foreach loop typically processes only a handful of customers per invocation,
   not the entire customer base at once.

3. **SQLite is single-writer.** The database is SQLite, which serializes all write operations behind a
   single lock. Parallel `SaveChangesAsync` calls, metric updates via `_systemInfo.SetAsync`, and
   `RenderDailySummaryAsync` (which queries the DB) would all contend on this lock. Parallelization
   would likely be slower, not faster, due to lock contention and retry overhead.

4. **AWS SES has its own rate limits.** SES sandbox accounts are limited to 1 email/second. Production
   SES accounts start at 14 emails/second. Firing off parallel SES calls without rate-limiting could
   trigger `ThrottlingException`, requiring backoff/retry logic — adding complexity to solve a problem
   that does not exist at this scale.

5. **The current error handling is clean and deliberate.** The sequential loop catches per-customer
   exceptions, logs them, and always advances `NextSendTimeUtc` to prevent retry storms. Parallelizing
   would require each task to have its own `IServiceScope` (for `DbContext` thread safety), independent
   error handling, and coordinated `SaveChangesAsync` — a substantial increase in complexity with no
   corresponding benefit.

6. **The scheduler already tolerates the timing.** The `MaxSleepDuration` is 1 minute. Even in a
   hypothetical scenario with 50 customers all due simultaneously (which the staggering mechanism
   prevents), sequential processing at roughly 300ms per email (render + SES round trip) would complete
   in about 15 seconds — well within operational tolerance.

**Conclusion:** The issue identifies a theoretical scalability concern that does not apply to this
system's architecture, scale, or deployment model. The proposed fix (parallel sending) would introduce
DbContext thread-safety hazards, SQLite lock contention, SES throttling risk, and error-handling
complexity — all to optimize a loop that processes a handful of customers in under a second.
