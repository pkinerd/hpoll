---
id: 24
title: "Tokens embedded in HTML page source on Hub detail page"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: high
---

## Description

**Severity: High**

In `Hubs/Detail.cshtml` lines 134-138, the raw `AccessToken`, `RefreshToken`, and `HueApplicationKey` values are embedded directly into the JavaScript source of the rendered page. Even though visually masked with bullet characters, the actual secret values are always present in the rendered HTML/JS.

Any XSS vulnerability elsewhere in the admin panel (or a browser extension with DOM access) could exfiltrate these tokens. The tokens are also stored in browser caches and history.

**Remediation:** Fetch tokens via a separate authenticated AJAX endpoint that returns the value only when explicitly requested. Alternatively, show only a truncated preview (last 8 characters) and require a dedicated server-round-trip "reveal" action.

## Comments

### claude — 2026-02-28

**Priority upgraded from medium to high** following comprehensive security review.

Additional technical risk identified: the token embedding pattern uses `Html.Raw(System.Net.WebUtility.HtmlEncode(...).Replace("'", "\\'"))` which is a fragile JavaScript string escape. If a token value contained a backslash followed by a single quote (`\'`), the replace would produce `\\'` which ends the string in JavaScript, enabling script injection. While Hue tokens are typically alphanumeric, the pattern itself is inherently unsafe and would be dangerous with less constrained data sources.

Without a Content-Security-Policy header (see #42), any XSS vulnerability would allow full exfiltration of all three tokens (application key, access token, refresh token) from the page DOM.
