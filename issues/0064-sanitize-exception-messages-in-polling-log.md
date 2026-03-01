---
id: 64
title: "Sanitize exception messages before persisting in PollingLog"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: low
---

## Description

When polling fails, the raw exception message (truncated to 500 characters) is stored in `PollingLog.ErrorMessage` and later displayed to admin users on the Hub Detail page.

**File:** `src/Hpoll.Worker/Services/PollingService.cs:250`
**Display:** `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml:122`

Exception messages from `HttpRequestException`, EF Core, and other providers can contain internal details such as connection strings, file paths, IP addresses, or partial credential information. Storing these verbatim in the database and displaying them in the admin UI creates an information disclosure risk.

**Recommended fix:**
- Use curated error messages for known exception types (e.g., "HTTP 401 Unauthorized", "Connection timeout")
- Store a generic message for unknown exception types (e.g., "Internal error during polling")
- Log the full exception details only to application logs (not the database)
- If the full message must be stored, ensure the admin UI does not display it verbatim

**OWASP reference:** A04:2021-Insecure Design

**Source:** Comprehensive review -- security review finding

## Comments

### claude — 2026-03-01

**Comprehensive review update:** In addition to the `PollingLog.ErrorMessage` issue, `HueApiClient` also logs error response bodies at `Warning` level in multiple locations:

- Lines 96-97: `GetMotionSensorsAsync` / `GetTemperatureSensorsAsync` (via `GetResourceAsync`)
- Lines 120-121: `GetDevicesAsync` / `GetDevicePowerAsync` (via `GetResourceAsync`)
- Lines 169-170: `GetResourceAsync` error path
- Lines 200-201: `PostTokenRequestAsync` error path

These responses could contain sensitive bridge configuration details, token fragments, or internal API error information. The error body truncation (`errorBody[..500]`) is applied, but the content itself may still be sensitive.

**Recommendation:** Log only HTTP status code and a generic error category at Warning level. Log the full response body at Debug level only, and ensure Debug logging is disabled in production.

### claude — 2026-03-01

**Consolidated from #0053 (closed as subset of this issue).**

Specific source-side fix from #0053: In `HueApiClient.RegisterApplicationAsync` line 141, the full JSON response body is embedded in an `InvalidOperationException` message:
```csharp
throw new InvalidOperationException($"Unexpected registration response format: {json}");
```
This is one concrete instance of the general problem described in this issue. The exception message surfaces in `PollingLog.ErrorMessage` and the admin UI. Fix by throwing a generic message and logging the full body at Debug level only.

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. Consolidation of #0053 was a mistake: RegisterApplicationAsync is only called from OAuthCallback, not during polling. HttpRequestException messages are already curated (no response bodies). Specific 401/429/503 catch blocks use curated messages. Only realistic vector is JsonException (sensor data, not credentials). Razor auto-encodes output. Consider reopening #0053 separately.

### critical-review — 2026-03-01

**Assessment: LOW_PRIORITY — the issue is technically valid but substantially overstated.**

#### What the code actually does

In `PollingService.PollHubAsync`, exception handling is layered in a disciplined way:

1. **401 Unauthorized** (line ~209): Caught specifically. `ErrorMessage` is set to the curated string `"Unauthorized (401) - token refreshed successfully, will retry next cycle"` or `"Unauthorized (401) - token refresh failed, hub marked as needs_reauth"`. No raw exception data persisted.
2. **429 Too Many Requests** (line ~227): Caught specifically. `ErrorMessage = "Rate limited (429)"`. Fully curated.
3. **503 Service Unavailable** (line ~234): Caught specifically. `ErrorMessage = "Bridge offline (503)"`. Fully curated.
4. **General catch-all** (line ~241): `log.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;` — this is the only path where raw exception text is persisted.

The three most common HTTP failure modes from the Hue API are already handled with curated messages. The general catch-all only fires for unexpected exceptions.

#### Do HttpClient/HttpRequestException messages leak tokens or credentials?

No. The .NET `HttpRequestException` message is constructed by the runtime as a status-code description (e.g., `"Response status code does not indicate success: 500 (Internal Server Error)"`). It does **not** include the request URL, headers, authorization tokens, or request/response bodies. The `HueApiClient` constructs its own `HttpRequestException` messages in `GetResourceAsync` and `PostTokenRequestAsync` — these contain only the API path and numeric status code (e.g., `"Hue API request failed for /resource/motion with status 404"`). The access token, application key, and refresh token are passed as method parameters and set as HTTP headers, but they never appear in the exception message text.

#### What could realistically appear in the catch-all?

The catch-all would fire for:
- **`JsonException`** — if the Hue API returns malformed JSON. The message would contain a fragment of the malformed response, which is sensor data (motion/temperature readings), not credentials.
- **`InvalidOperationException`** — from deserialization failures. Again, sensor data, not credentials.
- **`TaskCanceledException`** — timeout. Message is generic.
- **`SocketException`** / network errors — contain hostnames (`api.meethue.com`) and error codes, not credentials.
- **EF Core exceptions** — could contain the SQLite connection string (`Data Source=data/hpoll.db`), which is a local file path, not a remote credential. This is a very minor information disclosure.

None of these realistically leak OAuth tokens, refresh tokens, or user credentials.

#### Who sees the data?

The admin portal requires authentication via `ADMIN_PASSWORD_HASH` (cookie auth with rate limiting). All Razor Pages require authorization (`MapRazorPages().RequireAuthorization()`). The Hub Detail page already shows the actual access token, refresh token, and application key to the same admin user (with show/copy buttons). Therefore, even if an exception message somehow contained a token fragment, the admin user viewing PollingLog already has full access to those tokens via the same page.

Razor's `@(log.ErrorMessage ?? "")` auto-encodes HTML, so there is no XSS vector from stored exception messages.

#### Assessment of the consolidated #0053 claim

The comment consolidating #0053 is incorrect. `RegisterApplicationAsync` is called only from `OAuthCallback.cshtml.cs`, not from `PollingService`. Its `InvalidOperationException` (which embeds the JSON response body) would never be caught by `PollingService.PollHubAsync` and would never be persisted to `PollingLog.ErrorMessage`. The previous review correctly identified this error. Issue #0053 should be reopened as a separate concern scoped to the OAuth flow, not the polling path.

#### Assessment of the HueApiClient logging concern

The comment about `HueApiClient` logging error response bodies at Warning level is tangential to this issue. Those log entries go to the application logger (stdout/file), not to the SQLite database. They are not displayed in the admin UI. This is a separate concern (structured logging hygiene) and should not be conflated with the PollingLog persistence question.

#### Verdict

- **Status recommendation:** Downgrade to `wontfix` or keep as `low` priority with reduced scope.
- **Actual risk:** Minimal. The only realistic sensitive data that could appear in the catch-all is a local SQLite file path from EF Core exceptions, which is trivially discoverable information on the deployment host.
- **The three claimed threat vectors are not present:** (1) Tokens do not appear in .NET HttpRequestException messages; (2) the specific HTTP status catch blocks already use curated messages; (3) the admin user already has direct access to all tokens on the same page.
- **If any action is taken:** The most proportionate fix would be to change the general catch-all to store a generic message like `"Unexpected error: {ex.GetType().Name}"` and keep the full `ex.Message` only in the application log. This is a one-line change and addresses the theoretical EF Core path leak without the elaborate sanitization framework suggested in the original description.
