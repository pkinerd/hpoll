---
id: 82
title: "OnPostRegisterHubAsync OAuth handler entirely uncovered by tests"
status: open
created: 2026-03-01
author: claude
labels: [testing, security]
priority: medium
---

## Description

The `OnPostRegisterHubAsync` handler in `Customers/Detail.cshtml.cs` (lines 222-249) has 0%
test coverage. This handler:

- Generates a CSRF token and stores it in the session
- Constructs the Hue OAuth authorization URL with client_id, state parameter, and callback URL
- Redirects the user to the external Hue OAuth endpoint

This is security-sensitive code involving session state management and CSRF token generation
for OAuth flows. The entire block including session manipulation and URL construction is
untested.

**Found by:** Comprehensive review — code coverage analysis.

**Recommendation:** Add unit tests that mock `ISession` and `IOptions<HueAppSettings>` to
verify:
1. A CSRF token is stored in session state
2. The OAuth URL is correctly constructed with client_id, state, and callback URL
3. The response redirects to the Hue OAuth endpoint

## Comments
