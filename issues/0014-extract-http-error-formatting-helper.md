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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Line numbers stale (actual: 131-133, 159-161 in Detail.cshtml.cs; 152-154 in OAuthCallback.cshtml.cs). Pattern confined to Admin project only (2 files, 3 sites). Worker handles HttpRequestException completely differently. Target property differs (ErrorMessage vs Message). Borderline case for extraction.

### claude (critical review) — 2026-03-01

**Verdict: VALID but borderline — the duplication is real but the scope is narrow enough to question the value.**

**Verification of the claimed duplication:**

The issue correctly identifies 3 locations sharing the same ternary pattern. The actual line numbers on `main` are:

1. `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs` lines 131-133 (token refresh catch block):
   ```csharp
   ErrorMessage = ex.StatusCode.HasValue
       ? $"Token refresh failed: Hue API returned HTTP {(int)ex.StatusCode}."
       : "Token refresh failed: could not reach the Hue API.";
   ```

2. `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs` lines 159-161 (connection test catch block):
   ```csharp
   ErrorMessage = ex.StatusCode.HasValue
       ? $"Connection test failed: Hue API returned HTTP {(int)ex.StatusCode}."
       : "Connection test failed: could not reach the Hue API.";
   ```

3. `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs` lines 152-154 (hub registration catch block):
   ```csharp
   Message = ex.StatusCode.HasValue
       ? $"Hub registration failed: Hue API returned HTTP {(int)ex.StatusCode}."
       : "Hub registration failed: could not reach the Hue API.";
   ```

All three follow the exact same structural template: `ex.StatusCode.HasValue ? $"{action}: Hue API returned HTTP {(int)ex.StatusCode}." : $"{action}: could not reach the Hue API."`. The issue's description of the pattern is accurate.

**Line numbers in the original issue are wrong.** The issue cites lines 85-87, 113-115, and 151-153. The actual lines are 131-133, 159-161, and 152-154 respectively. This is a minor inaccuracy that does not affect the substance of the finding. The previous review already noted this.

**Scope analysis — is this pattern broader than stated?**

The issue claims the pattern exists only in the Admin project, and this is confirmed. However, the codebase also has related but structurally different `HttpRequestException` handling:

- `src/Hpoll.Core/Services/HueApiClient.cs` throws `HttpRequestException` at lines 98, 122, 172, and 202, but these are *throw sites* with different message formats (e.g., `"Enable link button failed with status {statusCode}"`, `"Hue API request failed for {path} with status {statusCode}"`). These are not the same pattern — they construct the exception, they do not format a user-facing message from one.

- `src/Hpoll.Worker/Services/PollingService.cs` catches `HttpRequestException` at lines 245, 270, 277 with exception filters (`when (ex.StatusCode == ...)`), but sets hardcoded `log.ErrorMessage` strings like `"Rate limited (429)"` and `"Bridge offline (503)"`. This is a completely different error-handling strategy — status-specific catch blocks with fixed messages, not a generic ternary format.

- `src/Hpoll.Worker/Services/TokenRefreshService.cs` catches a generic `Exception` (line 135), not `HttpRequestException` specifically, and only logs with `LogWarning` — no user-facing message formatting at all.

So the shared pattern genuinely exists only in the 3 Admin locations. The proposed helper `FormatHueApiError(string action, HttpRequestException ex)` would not unify anything across projects.

**Assessment of the proposed fix:**

The extraction would replace a 3-line ternary expression with a 1-line method call in each of 3 locations, totaling roughly 9 lines replaced by 3 call sites plus a ~5-line static helper method. The net code reduction is marginal (about 4 lines).

There are two minor complications:
1. In `Detail.cshtml.cs` the result is assigned to `ErrorMessage`, while in `OAuthCallback.cshtml.cs` it is assigned to `Message`. The helper would return a string, so both would still work, but this detail means the helper only standardizes the formatting, not the property assignment.
2. The helper would need to live somewhere accessible to both page models. A reasonable location would be a static utility class in the Admin project, but this creates a new file for a 5-line method.

**Conclusion:**

The duplication is real and correctly identified. However, with only 3 sites across 2 files in a single project, the benefit of extraction is minimal. The pattern is simple enough (a ternary operator) that inline repetition carries very little cognitive overhead or maintenance risk. If a fourth or fifth occurrence appeared, the case for extraction would strengthen. As it stands, this is a legitimate but low-impact cleanup — the "low" priority label is appropriate. Consider this a "nice to have" that should not be prioritized over more impactful work.
