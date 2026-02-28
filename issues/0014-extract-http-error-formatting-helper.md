---
id: 14
title: "Extract HttpRequestException error formatting helper"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

The same `HttpRequestException` error message formatting pattern appears 3 times in the Admin project:

- `Hubs/Detail.cshtml.cs` lines 85-87 (token refresh)
- `Hubs/Detail.cshtml.cs` lines 113-115 (connection test)
- `Hubs/OAuthCallback.cshtml.cs` lines 151-153 (hub registration)

All follow: `ex.StatusCode.HasValue ? $"{action}: Hue API returned HTTP {(int)ex.StatusCode}." : $"{action}: could not reach the Hue API."`

**Recommendation:** Add a small helper: `static string FormatHueApiError(string action, HttpRequestException ex)`.

## Comments
