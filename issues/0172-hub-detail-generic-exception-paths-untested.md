---
id: 172
title: "Hub Detail generic Exception and null-StatusCode error paths untested"
status: open
created: 2026-03-15
author: claude
labels: [testing]
priority: low
---

## Description

Several error handling paths in `Hubs/Detail.cshtml.cs` lack test coverage:

1. **`OnPostRefreshTokenAsync` generic Exception** (lines 133-137): The catch block for non-`HttpRequestException` errors sets a generic "unexpected error" message. No test exercises this path.

2. **`OnPostTestConnectionAsync` generic Exception** (lines 161-165): Same pattern — generic catch block untested.

3. **`OnPostRefreshTokenAsync` null StatusCode** (line 131): When `HttpRequestException.StatusCode` is null (e.g., DNS failure, network timeout), the message says "could not reach the Hue API". Tests only exercise the case where StatusCode has a value.

4. **`OnPostTestConnectionAsync` null StatusCode** (line 159): Same untested null-StatusCode branch.

**Location:** `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs`, lines 129-137, 157-165

**Category:** coverage-gap

**Severity:** low — these are trivial catch blocks that set a string message. Each is a single assignment statement with minimal logic to get wrong.

**Recommendation:** Add tests:
- `OnPostRefreshTokenAsync_GenericException_ShowsGenericErrorMessage`
- `OnPostTestConnectionAsync_GenericException_ShowsGenericErrorMessage`
- `OnPostRefreshTokenAsync_HttpExceptionNoStatusCode_ShowsCannotReachMessage`
- `OnPostTestConnectionAsync_HttpExceptionNoStatusCode_ShowsCannotReachMessage`

## Comments
