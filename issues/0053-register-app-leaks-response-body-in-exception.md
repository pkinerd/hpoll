---
id: 53
title: "RegisterApplicationAsync leaks full Hue API response body in exception message"
status: open
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
