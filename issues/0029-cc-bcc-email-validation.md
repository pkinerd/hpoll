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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Recommend relabeling security->enhancement. Admin portal requires authentication (line 90). SES validates and rejects malformed emails. Primary To field has same validation gap (not mentioned). [EmailAddress] attribute cannot validate comma-delimited strings. This is a UX/data quality concern, not a security vulnerability.

### critical-review — 2026-03-01

**Assessment: PARTIALLY_VALID — downgrade to enhancement, not a security issue.**

**What the issue gets right:**

The CC and BCC fields (`EditCcEmails`, `EditBccEmails` in `Detail.cshtml.cs`) genuinely lack
server-side format validation. In `OnPostUpdateEmailsAsync`, the values are trimmed and saved
directly with no email format check:

```csharp
customer.CcEmails = (EditCcEmails ?? string.Empty).Trim();
customer.BccEmails = (EditBccEmails ?? string.Empty).Trim();
```

There are no `[EmailAddress]`, `[RegularExpression]`, or custom validation attributes on these
bind properties. So yes, arbitrary text can be persisted to the database.

**What the issue gets wrong or overstates:**

1. **"Security" label is inappropriate.** The admin portal is an internal tool, not a
   public-facing surface. Only authenticated administrators can set these fields. There is no
   injection vector, no XSS path, and no privilege escalation from storing a malformed email
   string. The `security` label should be changed to `enhancement` or `data-quality`.

2. **The issue singles out CC/BCC but ignores the primary Email field.** The `Email` field on
   Customer (and `EditEmail` on the Detail page) has the same absence of `[EmailAddress]`
   validation. The `Email` property on `CreateModel` uses `[Required, StringLength(500)]` but
   no email format check. The issue description is inconsistent by flagging only CC/BCC.

3. **The `Contains('@')` check in `ParseEmailList` is a runtime guard, not a persistence guard.**
   The issue conflates two layers. `EmailSchedulerService.ParseEmailList` filters out entries
   without `@` at send time, so completely non-email strings would be silently dropped rather
   than sent to SES. This means the worst case for malformed CC/BCC data is that the intended
   recipient does not receive the email -- a silent failure, not a security issue.

4. **SES itself rejects truly malformed addresses.** Even if a string like `foo@bar` passes the
   `Contains('@')` check, AWS SES validates the address at send time and will return an error
   for addresses that are not RFC-compliant. The `SesEmailSender` catches and logs these
   exceptions. So there is a second layer of runtime protection.

5. **The proposed remediation (`[EmailAddress]` attribute) does not work for comma-separated
   fields.** The `[EmailAddress]` data annotation validates a single email address. Since
   `CcEmails` and `BccEmails` are comma-separated lists, applying `[EmailAddress]` directly
   would reject all valid multi-address inputs. A custom validation attribute or manual parsing
   plus per-address validation would be needed, which the issue does not acknowledge.

**Actual risk:**

- An admin enters a typo like `user@exmaple` in the CC field.
- The value is persisted to SQLite.
- At email send time, `ParseEmailList` splits and filters by `@`, so the typo passes through.
- SES attempts delivery and either bounces (if the domain exists but mailbox does not) or
  rejects (if the address is syntactically invalid per RFC).
- The error is logged but the primary To recipients still receive their email.

This is a **data quality / UX concern** -- providing early feedback to the admin that they typed
an invalid address, rather than discovering it later in logs. It is not a security vulnerability.

**Recommendation:** Relabel from `security` to `enhancement`, keep priority `low`. If
implemented, write a custom validation helper that splits on commas and validates each entry
individually, rather than using `[EmailAddress]` on the whole field.
