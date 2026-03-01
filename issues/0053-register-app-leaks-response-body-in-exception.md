---
id: 53
title: "RegisterApplicationAsync leaks full Hue API response body in exception message"
status: closed
created: 2026-03-01
author: claude
labels: [bug, security]
priority: medium
---

## Description

In `HueApiClient.RegisterApplicationAsync`, when the bridge returns an unexpected response format, the full JSON response body is included in the exception message at line 141:

```csharp
throw new InvalidOperationException($"Unexpected registration response format: {json}");
```

This exception message could surface in logs, error pages, or admin UI polling logs and might contain sensitive information from the bridge's response.

**File:** `src/Hpoll.Core/Services/HueApiClient.cs:141`

**Recommended fix:** Truncate or sanitize the response body before including it in the exception (e.g., first 200 chars), or log the full body separately at Warning level and throw a generic message.

**Source:** Security review finding S7.1

## Comments

### claude — 2026-03-01

**Comprehensive review note:** This pattern extends beyond `RegisterApplicationAsync`. The security review identified similar sensitive data exposure in API error body logging at multiple locations in `HueApiClient.cs`:

- Lines 96-97: `EnableLinkButtonAsync` logs error body at Warning level
- Lines 120-121: `RegisterApplicationAsync` logs error body at Warning level
- Lines 169-170: `GetResourceAsync` logs error body at Warning level
- Lines 200-201: `PostTokenRequestAsync` logs error body at Warning level (token endpoint responses are especially sensitive)

The error bodies are truncated to 500 characters but Hue API error responses could contain partial tokens, application keys, or internal bridge details. Token endpoint responses in particular should never be logged even at Warning level.

**Recommendation:** Log only the HTTP status code at Warning level. Move full error body logging to Debug level. Never log token endpoint response bodies.

### claude — 2026-03-01

**Closed:** Consolidated into #0064 (Sanitize exception messages before persisting in PollingLog). This issue describes one specific instance of the general information disclosure problem covered by #0064. The `RegisterApplicationAsync` exception message is one of several locations where sensitive data flows into `PollingLog.ErrorMessage` and the admin UI.
