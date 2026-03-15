---
id: 167
title: "OAuth callback reflects error query parameter on AllowAnonymous page"
status: open
created: 2026-03-15
author: claude
labels: [security]
priority: low
---

## Description

When the Hue OAuth flow returns an error, the `OAuthCallback` page reflects the raw `error` query parameter directly into the page content:

```csharp
Message = $"Hue authorization was denied: {error}";
```

While Razor's `@Model.Message` HTML-encodes the output (preventing XSS), the `OAuthCallback` page is marked `[AllowAnonymous]`, meaning any unauthenticated user can craft a URL with arbitrary `error` values. This is a content injection / social engineering vector — an attacker could send a link like:

```
/Hubs/OAuthCallback?error=Your+account+has+been+compromised.+Contact+support+at+evil.com
```

The message would render on the admin portal page. However, the practical impact is limited: the admin portal is an internal tool with a narrow audience, the message appears in a red `alert-error` div prefixed with "Hue authorization was denied:", and the OAuth 2.0 spec constrains the `error` parameter to a small set of ASCII codes — so attacker-controlled text only appears via direct URL manipulation, not through the standard OAuth flow.

Related: #72 / #65 previously addressed the broader `AllowAnonymous` access control concern on this page.

**Location:** `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs`, line 45

**Category:** injection (content injection)

**Severity:** low — not XSS due to Razor encoding, and limited practical impact on an internal admin page. Defense-in-depth improvement.

**OWASP reference:** A03:2021 - Injection

**Recommendation:** Do not reflect the raw `error` parameter. Map OAuth error codes to a fixed set of known messages:

```csharp
Message = error switch
{
    "access_denied" => "Hue authorization was denied by the user.",
    _ => "Hue authorization failed. Please try again."
};
```

This prevents arbitrary attacker-controlled text from appearing on the page.
