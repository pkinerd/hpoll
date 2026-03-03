---
id: 14
title: "Extract HttpRequestException error formatting helper"
status: closed
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: low
closed: 2026-03-03
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

### claude (critical review) — 2026-03-01

**Verdict: CLOSE — the duplication is real but too trivial and narrow to warrant extraction.**

**Misleading title and framing.** The title "Extract HttpRequestException error formatting helper" implies the duplication lives in how `HttpRequestException` objects are *constructed* (i.e., in `HueApiClient`). In reality, the 4 throw sites in `HueApiClient.cs` (lines 98, 122, 172, 202) each use intentionally different message strings — "Enable link button failed", "Register application failed", "Hue API request failed for {path}", "Token request failed" — and share no extractable pattern. The actual duplication is in how `HttpRequestException` is *caught and formatted for user display* in the Admin Razor pages. The issue body describes this correctly, but the title creates a false impression about scope and location.

**The duplicated pattern is a UI concern, not an HTTP concern.** The 3 catch sites produce user-facing strings shown in the admin portal. Each already carries a distinct action prefix ("Token refresh failed", "Connection test failed", "Hub registration failed") that is essential for the user to understand what went wrong. A helper like `FormatHueApiError(string action, HttpRequestException ex)` would parameterize the action string — but parameterizing a single ternary expression with one variable part produces a helper that is nearly as long as the code it replaces:

```csharp
// Helper (5 lines including signature)
static string FormatHueApiError(string action, HttpRequestException ex)
    => ex.StatusCode.HasValue
        ? $"{action}: Hue API returned HTTP {(int)ex.StatusCode}."
        : $"{action}: could not reach the Hue API.";

// Call site (1 line, saves 2 lines per site)
ErrorMessage = FormatHueApiError("Token refresh failed", ex);
```

Net savings: approximately 6 lines across the entire codebase (3 sites x 2 lines saved, minus 5 lines for the helper definition plus a new file or class). This is negligible.

**Premature abstraction risk.** With only 3 call sites, extracting a helper creates a coupling point. If any site later needs richer formatting — including the response body, varying the wording, adding retry guidance, or distinguishing between different HTTP status codes — the helper either grows parameters for every variation or the site reverts to inline code. The Worker project already demonstrates this divergence: `PollingService` uses status-specific catch filters with hardcoded messages, showing that error-handling strategies naturally differ by context. Locking the Admin sites into a shared helper assumes their formatting needs will evolve in lockstep, which is unlikely.

**Error messages should remain context-specific for debuggability.** The current inline ternary expressions are immediately readable at the catch site. A developer investigating a user-reported error can see exactly what message is produced without navigating to a helper method. This is a minor but real advantage for a codebase with only 3 occurrences.

**Recommendation: close this issue.** The duplication is real but falls below the threshold where extraction provides meaningful benefit. The rule of three is a guideline, not a mandate — especially when the repeated code is a single expression and the "common" part is boilerplate rather than logic. If the Admin project grows additional catch sites using this pattern (a fourth or fifth occurrence), the case strengthens and the issue can be reopened. Until then, the inline approach is clearer, more maintainable, and carries no abstraction overhead.

### claude — 2026-03-03

**Closed as wontfix.** Multiple reviews agree the duplication is real but too narrow (3 sites, 2 files, single project) and too trivial (a one-line ternary) to justify extracting a helper. Net code savings would be ~6 lines, the helper introduces premature abstraction risk, and inline expressions are more debuggable. Can be reopened if additional catch sites appear.
