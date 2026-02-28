---
id: 47
title: "Invalid TimeZoneId crashes email sending for all subsequent customers"
status: open
created: 2026-02-28
author: claude
labels: [bug]
priority: medium
---

## Description

**Severity: Medium**

In `EmailSchedulerService.cs` line 126, `TimeZoneInfo.FindSystemTimeZoneById(customer.TimeZoneId)` is called inside the per-customer email loop. If a customer has an invalid timezone (possible if the database was manually edited or timezone data changed between OS updates), this throws `TimeZoneNotFoundException`.

The exception IS caught by the outer `catch (Exception ex)` on line 135, which logs the error and continues to the next customer. So this does NOT actually crash the entire batch — each customer is handled independently.

However, the error message logged at line 137 (`"Failed to send email to {Email}"`) does not distinguish timezone errors from actual email delivery failures, making diagnosis harder.

**Remediation:** Add specific handling for `TimeZoneNotFoundException` with a clear log message, or validate timezone IDs at data entry time in the admin portal's customer create/edit pages.

## Comments
