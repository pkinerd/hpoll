---
id: 132
title: "Email addresses logged in plaintext — compliance consideration"
status: closed
closed: 2026-03-03
created: 2026-03-03
author: claude
labels: [code-quality]
priority: low
---

## Description

Customer email addresses are logged in plaintext in multiple places:

- `src/Hpoll.Worker/Services/EmailSchedulerService.cs` line 148:
  `"Failed to send email to {Email} (customer {Name}, Id={Id})"`
- `src/Hpoll.Worker/Services/EmailSchedulerService.cs` line 195:
  `"Email sent to {Email} (customer {Name}, Id={Id})"`
- `src/Hpoll.Email/SesEmailSender.cs` line 59:
  `"Email sent to {To}, MessageId: {MessageId}"`
- `src/Hpoll.Email/SesEmailSender.cs` line 63:
  `"Failed to send email to {To}"`

Customer names (`{Name}`) are also PII and logged alongside email addresses.

**Mitigating factors:**
- This is a self-hosted, single-operator monitoring service with console-only logging (no
  external log aggregation configured)
- The same email addresses are already stored in plaintext in the SQLite database
- The operator who sees logs is the same person who entered the email addresses
- Email addresses in logs serve a legitimate operational debugging purpose (diagnosing
  delivery failures)

This is a compliance consideration (GDPR Article 5(1)(c) data minimisation) rather than a
security vulnerability. It would become relevant if logs are shipped to a third-party
aggregation service or if the deployment model changes to multi-tenant.

**Recommendation:** Revisit if logging infrastructure changes (e.g., centralized log
aggregation). For current architecture, the email addresses serve operational debugging needs.

**Found by:** Comprehensive review — security review.

## Comments

### claude — 2026-03-03

Fixed in commit on branch `claude/fix-multiple-issues-OnW6X`.
