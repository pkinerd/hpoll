---
id: 99
title: "EmailSchedulerService lacks test for renderer failure path"
status: open
created: 2026-03-02
author: claude
labels: [testing]
priority: low
---

## Description

**Note:** The existing `ContinuesOnSingleCustomerFailure` test already covers the same `try/catch` block via a sender exception. A renderer exception follows the exact same code path through the same catch block. The value of this additional test is primarily in verifying that the sender is correctly **skipped** when the renderer fails (different from the sender failure case where both renderer and sender are invoked).

The `EmailSchedulerService` test suite has `ProcessDueCustomers_ContinuesOnSingleCustomerFailure` which tests what happens when the **sender** throws, but there is **no test** for what happens when the **renderer** throws.

If `IEmailRenderer.RenderDailySummaryAsync()` throws an exception (e.g., database error, invalid timezone, malformed data), the service should catch it and continue to the next customer. This behavior is untested.

**Location:** `tests/Hpoll.Worker.Tests/EmailSchedulerServiceTests.cs`

**Recommendation:**
Add a test that configures `_mockRenderer` to throw on the first customer and succeed on the second, then verify:
1. Both customers were attempted (not short-circuited on first failure)
2. The second customer's email was sent successfully
3. The first customer's `NextSendTimeUtc` was still advanced (to prevent retry loops)

## Comments
