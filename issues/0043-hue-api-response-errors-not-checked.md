---
id: 43
title: "Hue API response errors array is never checked after deserialization"
status: open
created: 2026-02-28
author: claude
labels: [bug]
priority: low
---

## Description

**Severity: Medium**

In `HueApiClient.cs` `GetResourceAsync` (line 179), the deserialized `HueResponse<T>` contains an `Errors` list that is never inspected. Per the Hue CLIP API v2 error handling documentation, even HTTP 200 responses can contain errors in the `errors` array, and HTTP 207 responses indicate partial success with some operations failing.

The current code only checks `response.IsSuccessStatusCode` and then returns the deserialized response without validation:

```csharp
var result = JsonSerializer.Deserialize<HueResponse<T>>(json, JsonOptions);
return result ?? throw new InvalidOperationException(...);
// result.Errors is never checked
```

**Impact:** Partial API failures or warnings from the Hue Bridge are silently ignored. Motion/temperature data could be incomplete without any indication in logs.

**Remediation:** After deserialization, check `if (result.Errors.Count > 0)` and log them. For critical endpoints, consider throwing on errors.

## Comments

### claude — 2026-03-01 (critical review)

**Verdict: VALID — low severity, low priority. The core claim is accurate but the issue overstates the impact.**

#### What the code review confirms

1. **The `Errors` array is genuinely never checked in production code.** In `HueApiClient.GetResourceAsync` (line 179), the deserialized `HueResponse<T>` is returned directly to callers without any inspection of `result.Errors`. All four callers in `PollingService.PollHubAsync` (lines 124-137) only access `.Data` and never look at `.Errors`. The admin portal callers (`Detail.cshtml.cs` line 152, `OAuthCallback.cshtml.cs` line 134) similarly only use `.Data.Count`.

2. **The `HueError` model and `Errors` property are correctly deserialized** — unit tests in `HueApiModelsTests.cs` verify both empty errors on success (lines 43, 114, 169, 232) and populated errors (lines 292-294), confirming the model works. The code just never acts on the deserialized errors at runtime.

#### Where the issue overstates the problem

1. **The HTTP 207 claim is misleading.** The issue states "HTTP 207 responses indicate partial success with some operations failing." HTTP 207 (Multi-Status) is relevant to PUT/POST operations that modify multiple resources. This application only uses GET requests via `GetResourceAsync` for read-only sensor polling. GET requests return all resources of a type; there is no "partial success" scenario for a GET that lists motion sensors. The HTTP 207 claim is technically accurate for the Hue API in general but irrelevant to this codebase.

2. **The "Severity: Medium" label is too high.** The Hue CLIP v2 API communicates most real errors via HTTP status codes (401 Unauthorized, 429 Too Many Requests, 503 Service Unavailable), which `GetResourceAsync` already handles by throwing `HttpRequestException`. The `errors` array in a 200 response for GET endpoints is primarily a structural envelope field that is almost always empty. When the bridge genuinely cannot serve a request, it returns non-2xx status codes, not a 200 with populated errors.

3. **Real-world impact is minimal for GET polling.** If the Hue bridge did return errors in a 200 response for a GET request, the `data` array would typically be empty or incomplete. The `PollingService` iterates over `.Data` with `foreach`, so empty data simply means zero iterations — no crash, no corrupt data stored. The only subtle issue is the success log at line 243 (`"polled successfully. {MotionCount} motion, {TempCount} temperature readings"`) which would log `0 motion, 0 temperature` without indicating that errors were present. This is a diagnostic gap, not a data integrity issue.

#### What the fix should look like

The remediation suggestion is sound: add a warning log in `GetResourceAsync` when `result.Errors.Count > 0`. This is a ~3-line change:

```csharp
if (result.Errors.Count > 0)
{
    _logger.LogWarning("Hue API response for {Path} contained {ErrorCount} error(s): {Errors}",
        path, result.Errors.Count, string.Join("; ", result.Errors.Select(e => e.Description)));
}
```

There is no need to throw on errors for GET endpoints — the callers already handle empty data gracefully. Throwing would be counterproductive since it would prevent processing of any valid data that might coexist with errors in the response.

#### Summary

- **Core claim (errors never checked): TRUE**
- **HTTP 207 partial success claim: MISLEADING** — not applicable to GET-only usage
- **Severity: Low** (not Medium as stated) — missing a log warning, not silently losing data
- **Priority: Low** — a minor observability improvement, not a bug that affects correctness
- **Labels: Should be `enhancement` rather than `bug`** — the current behavior is not incorrect, just insufficiently observable
