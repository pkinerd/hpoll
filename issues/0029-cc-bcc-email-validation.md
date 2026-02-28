---
id: 29
title: "CC/BCC email fields lack server-side format validation"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: low
---

## Description

**Severity: Low**

In `Customers/Detail.cshtml.cs` lines 108-124, CC and BCC email fields are stored with no server-side validation beyond the `Contains('@')` check in `EmailSchedulerService.ParseEmailList`. Malformed email-like strings can be passed to SES.

**Remediation:** Apply `[EmailAddress]` validation or regex pattern to individual CC/BCC entries on the server side before persisting.

## Comments
