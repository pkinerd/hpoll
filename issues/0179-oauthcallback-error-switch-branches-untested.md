---
id: 179
title: "OAuthCallback error mapping switch branches mostly untested"
status: closed
closed: 2026-03-15
created: 2026-03-15
author: claude
labels: [testing]
priority: low
---

## Description

The `OnGetAsync` method in `OAuthCallbackModel` contains a `switch` expression (lines 46-54) that maps OAuth error codes to user-friendly messages. This switch was introduced by issue #167 as a security fix to prevent reflecting raw error query parameters. It handles `access_denied`, `unauthorized_client`, `invalid_request`, `unsupported_response_type`, `server_error`/`temporarily_unavailable`, and a default fallback. Only the `access_denied` branch is exercised by tests. The remaining 5 branches plus the default lack test coverage.

Branch coverage for `OAuthCallback.cshtml.cs` is 69.4%, and these untested switch branches contribute to the gap.

Note: these are pure string-literal-to-string-literal mappings with no conditional logic, so the practical risk of a bug is minimal. This is a low-value coverage gap worth closing only if there is a broader effort to improve branch coverage metrics.

**Location:** `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs`, lines 46-54

**Category:** coverage-gap

**Severity:** low — trivial string mappings with no logic; low risk of defects.

**Recommendation:** Add a `[Theory]` test with `[InlineData]` for each error code, verifying the correct user-friendly message is returned for each case.

## Comments

### claude — 2026-03-15

Fixed: Replaced single `access_denied` Fact with a `[Theory]` covering all 7 switch branches (access_denied, unauthorized_client, invalid_request, unsupported_response_type, server_error, temporarily_unavailable, and default fallback). All 494 tests pass.
