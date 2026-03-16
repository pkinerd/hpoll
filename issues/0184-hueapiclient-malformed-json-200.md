---
id: 184
title: "HueApiClient logs unclear error on malformed JSON 200 response"
status: open
created: 2026-03-16
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

When the Hue API returns HTTP 200 with an invalid JSON body (e.g., truncated response, HTML error page from a proxy), `JsonSerializer.DeserializeAsync` throws a `JsonException` that propagates up to the caller.

**Location:** `src/Hpoll.Core/Services/HueApiClient.cs` — `GetResourceAsync` and `PostTokenRequestAsync`

**Impact:** Low — the `JsonException` is caught by the generic `catch (Exception ex)` in `PollHubAsync` (PollingService.cs line 280), so the polling cycle does not crash. However, the logged error message is a raw `JsonException` stack trace rather than a clear "malformed response from Hue API" warning, making troubleshooting harder.

**Found by:** Unit testing review — missing edge case (finding 2.2)

**Recommendation:** Consider wrapping the deserialization in a try-catch for `JsonException` to log a clearer warning with the response body snippet before re-throwing or returning null. Add a test returning 200 OK with invalid JSON to verify behavior.

## Comments
