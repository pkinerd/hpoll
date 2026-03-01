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

### claude (critical review) — 2026-03-01

**Assessment: VALID as a data quality concern. Agree this is NOT a security issue. However, the previous review understates the operational risk.**

**Confirming the core finding:**

The issue correctly identifies that CC/BCC fields have no server-side format validation. In `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs`, the `EditCcEmails` (line 38-39) and `EditBccEmails` (line 41-42) bind properties have `[BindProperty]` but no validation attributes whatsoever. The `OnPostUpdateEmailsAsync` method (lines 104-122) checks `ModelState.IsValid` at line 111, but since there are no validation attributes on these properties, ModelState will always pass for them. The values are persisted directly:

```csharp
// Detail.cshtml.cs lines 114-115
customer.CcEmails = (EditCcEmails ?? string.Empty).Trim();
customer.BccEmails = (EditBccEmails ?? string.Empty).Trim();
```

Notably, `EditEmail` (the primary To field, line 32-33) also has no `[EmailAddress]` or `[Required]` attribute, confirming the previous reviewer's point that this gap is not unique to CC/BCC.

**Correcting a factual error in the previous review:**

The previous critical-review comment states: "the primary To recipients still receive their email" when CC/BCC contains malformed addresses. **This is incorrect.** Examining the send path:

1. `EmailSchedulerService.SendCustomerEmailAsync` (line 169-190 of `src/Hpoll.Worker/Services/EmailSchedulerService.cs`) builds To, CC, and BCC lists, then calls `sender.SendEmailAsync(toList, subject, html, ccList, bccList, ct)` in a **single API call** (line 186).

2. `SesEmailSender.SendEmailAsync` (line 31-66 of `src/Hpoll.Email/SesEmailSender.cs`) constructs a single `SendEmailRequest` with all recipients in one `Destination` object (lines 33-39), then calls `_sesClient.SendEmailAsync(sendRequest, ct)` at line 58.

3. If SES rejects the request because any address in CC or BCC is syntactically invalid (e.g., `not-an-email`), the entire `SendEmailAsync` call throws. The exception propagates from `SesEmailSender` (re-thrown at line 64) to `SendCustomerEmailAsync`, and is caught in `ProcessDueCustomersAsync` at line 140.

4. **Consequence: a malformed CC/BCC address causes the entire email to fail for ALL recipients, including the primary To addresses.** The customer receives no daily summary email at all. Furthermore, since `ProcessDueCustomersAsync` always advances `NextSendTimeUtc` (lines 146-150), the failed send is not retried -- the email is simply lost for that cycle.

This is a meaningful operational risk, not just "the intended recipient does not receive the email." It silently prevents delivery to *all* recipients for that customer.

**Nuance on the `Contains('@')` filter:**

`ParseEmailList` at line 217-225 does filter out entries without `@`. So a completely non-email string like `"hello world"` would be dropped. However, strings like `"@"`, `"foo@"`, `"@bar"`, or `"foo @bar.com baz"` (with embedded spaces after comma splitting) would pass the `Contains('@')` check but are not valid RFC 5321 addresses. The `.TrimEntries` in the split handles leading/trailing whitespace, but `"foo @bar"` as a single entry would still pass.

More importantly, a string like `"not-real@nonexistent.tld"` passes all current checks but will bounce at the SMTP level -- this is not caught by SES at send time (SES accepts the request and the bounce occurs asynchronously). High bounce rates can damage the SES sender reputation, potentially leading to SES throttling or suspension of the sending account. This is documented in AWS SES best practices.

**Precise scope of the gap:**

| Field | Create page validation | Detail page validation | Runtime guard |
|-------|----------------------|----------------------|---------------|
| Email (To) | `[Required, StringLength(500)]` only (`src/Hpoll.Admin/Pages/Customers/Create.cshtml.cs` line 26) | No attributes (`Detail.cshtml.cs` line 32-33) | `ParseEmailList` filters by `@` |
| CcEmails | N/A (not on Create page) | No attributes (`Detail.cshtml.cs` line 38-39) | `ParseEmailList` filters by `@` |
| BccEmails | N/A (not on Create page) | No attributes (`Detail.cshtml.cs` line 41-42) | `ParseEmailList` filters by `@` |

**Agreement on label change:**

This is not a security vulnerability. The admin portal requires authentication (`Program.cs` line 90: `app.MapRazorPages().RequireAuthorization()`), uses antiforgery tokens (line 68), and the login system has rate limiting (`Login.cshtml.cs` lines 42-50). Only authenticated administrators can set these fields. There is no injection vector or privilege escalation.

**Recommendation:** Relabel `security` to `enhancement`. Upgrade priority from `low` to `medium` given the operational risk that a single bad CC/BCC address silently blocks delivery to all recipients for that customer. The fix should be a custom validation helper in `OnPostUpdateEmailsAsync` that splits on commas and validates each entry with `System.Net.Mail.MailAddress.TryCreate()` (available in .NET 8) before persisting. The same validation should be applied to the primary `EditEmail` field for consistency.
