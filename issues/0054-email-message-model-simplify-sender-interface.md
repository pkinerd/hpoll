---
id: 54
title: "Introduce EmailMessage model to simplify IEmailSender interface"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, code-quality]
priority: medium
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
