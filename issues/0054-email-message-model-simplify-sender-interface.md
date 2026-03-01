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

### claude — 2026-03-01

**Detailed critical review: recommend CLOSE as won't-fix.**

#### Factual Errors in the Issue

1. **Wrong parameter types quoted.** The issue shows `string toAddress` but the actual interface uses `List<string> toAddresses`. The proposed `EmailMessage` model with `string ToAddress` would be a type regression that loses multi-recipient support already present in the interface.

2. **Parameter count is overstated.** The issue says "4-6 parameters each." The simpler overload has 3 real parameters plus CancellationToken (which is a standard .NET convention, not a domain parameter). The fuller overload has 5 real parameters plus CancellationToken. Counting CancellationToken as a complexity-driving parameter is misleading -- it appears on virtually every async method in .NET.

#### Cost-Benefit Analysis

**Costs of this change:**
- New model class file to maintain.
- Modifications to `IEmailSender` (interface), `SesEmailSender` (implementation), and `EmailSchedulerService` (caller).
- Rewriting 7 unit tests in `SesEmailSenderTests.cs` and at least 10 Moq setups/verifies in `EmailSchedulerServiceTests.cs`, all of which currently match on individual parameters. With a parameter object, Moq verification becomes harder -- you must use `It.Is<EmailMessage>(m => m.ToAddresses.Contains(...) && m.Subject == ... && ...)` which is arguably less readable than the current individual parameter matching.
- Net increase in total lines of code with no behavioral change.

**Benefits claimed:**
- "Simpler interface" -- but collapsing two overloads into one method with a parameter object is not simpler; it moves complexity from the method signature to the object initializer at the call site.
- "Future extensibility" for reply-to, attachments, importance flags -- these are speculative features (YAGNI). If they are needed later, the refactoring can be done at that time with full knowledge of the actual requirements.

#### Single-Caller Analysis

There is exactly **one production caller**: `EmailSchedulerService.SendCustomerEmailAsync` at line 186 of `EmailSchedulerService.cs`. The call site is:

```csharp
await sender.SendEmailAsync(toList, subject, html, ccList, bccList, ct);
```

With the proposed model, this becomes:

```csharp
await sender.SendEmailAsync(new EmailMessage
{
    ToAddresses = toList,
    Subject = subject,
    HtmlBody = html,
    CcAddresses = ccList,
    BccAddresses = bccList,
}, ct);
```

This is more verbose, not simpler. The named parameters in the object initializer add clarity that could be achieved with zero cost via IDE parameter hints on the existing method. The "introduce parameter object" refactoring pattern is appropriate when a method has 7+ parameters, when the same parameter group appears across multiple methods, or when there are multiple callers that would benefit from a shared construction pattern. None of these conditions hold here.

#### The Overload Pattern Is Already Clean

The existing two-overload design is a well-established .NET pattern. The simpler 3-parameter overload delegates to the fuller 5-parameter overload, which is exactly how the BCL handles this (e.g., `HttpClient.SendAsync`). The shorter overload exists so that callers who do not need CC/BCC do not have to pass nulls. This is idiomatic and maintainable.

#### Verdict

**Close as won't-fix.** The issue contains factual errors (wrong types), overstates the parameter count problem, proposes a model that would regress type safety, and the refactoring would increase code volume across 4+ files and 10+ test setups for zero behavioral benefit in a single-caller scenario. If the interface ever genuinely needs 7+ parameters or gains multiple callers with different construction patterns, the parameter object refactoring can be revisited at that time.
