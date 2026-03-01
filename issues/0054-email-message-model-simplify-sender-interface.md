---
id: 54
title: "Introduce EmailMessage model to simplify IEmailSender interface"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

The `IEmailSender` interface has two overloads with 4-6 parameters each:

```csharp
Task SendEmailAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default);
Task SendEmailAsync(string toAddress, string subject, string htmlBody, List<string>? ccAddresses, List<string>? bccAddresses, CancellationToken ct = default);
```

This is parameter sprawl — adding future email features (reply-to, attachments, importance flag) would require yet another overload.

**Files:**
- `src/Hpoll.Core/Interfaces/IEmailSender.cs`
- `src/Hpoll.Email/SesEmailSender.cs`

**Recommended fix:** Introduce an `EmailMessage` model class:

```csharp
public class EmailMessage
{
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public List<string>? CcAddresses { get; set; }
    public List<string>? BccAddresses { get; set; }
}
```

Simplify the interface to a single method: `Task SendEmailAsync(EmailMessage message, CancellationToken ct = default)`.

**Source:** Code quality review finding Q11

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. **Factual error**: interface uses List<string> toAddresses (plural), not string toAddress. The proposed EmailMessage model with string ToAddress would be a regression. Only 1 production caller exists. The YAGNI extensibility argument (reply-to, attachments) is speculative. The EmailMessage model just moves parameters to an object initializer without reducing complexity.
