---
id: 65
title: "OAuth callback should use signed state parameter instead of session-based CSRF"
status: open
created: 2026-03-01
author: claude
labels: [security, enhancement]
priority: low
---

## Description

The OAuth callback endpoint (`OAuthCallback.cshtml.cs`) is `[AllowAnonymous]` and relies on an in-memory session-stored CSRF token (`OAuthCsrf`) for validation. This has several limitations:

1. **Session dependency:** If the session expires (30-minute timeout) between OAuth initiation and callback, the CSRF check fails and the authorization code is wasted
2. **Multi-instance incompatibility:** The distributed memory cache is per-instance, so in a multi-instance deployment, the callback could be routed to a different instance that lacks the session state
3. **Session fixation risk:** The CSRF token's security depends entirely on the session infrastructure

**File:** `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs:48-61`

**Recommended fix:**
- Use a cryptographically signed state parameter (e.g., using ASP.NET Data Protection API) that embeds the customer ID and a timestamp
- Validate the signature and timestamp on callback instead of looking up session state
- This eliminates the session dependency and works across multiple instances
- Add an explicit expiration (e.g., 10 minutes) to the signed state

**OWASP reference:** A07:2021-Identification and Authentication Failures

**Source:** Comprehensive review -- security review finding

## Comments

### claude — 2026-03-01

**Consolidated from #0072 (closed as subset of this issue).**

Additional acceptance criteria from #0072's access control analysis:

- **Explicit auth validation:** The signed state parameter should embed the authenticated user's identity (e.g., admin user ID), so the callback inherently validates the request originated from an authenticated session — addressing the `[AllowAnonymous]` concern.
- **Null CSRF check:** Validate that `expectedCsrf` is non-null before comparing (currently a null session value could pass comparison).
- **Expired session handling:** If the signed state is expired, redirect to login with a clear error message rather than showing a generic CSRF failure.

The signed state approach resolves both the CSRF mechanism weakness (this issue) and the access control gap (#0072) in a single implementation.

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. Recommend relabeling security,enhancement->enhancement. The null CSRF claim is **wrong**: code checks expectedCsrf==null first (line 58). Multi-instance concern is speculative (single container). Session cookies properly hardened. [AllowAnonymous] is necessary (commit d0de72e fixed this). Current CSRF implementation is textbook correct (128-bit GUID). Signed state is an architectural preference, not a security fix.

### claude — 2026-03-01

**Critical review: INVALID — Recommend closing as Won't Fix.**

After reviewing the full OAuth flow across `Detail.cshtml.cs` (state generation in `OnPostRegisterHubAsync`) and `OAuthCallback.cshtml.cs` (state validation in `OnGetAsync`), this issue does not identify a real security vulnerability. The proposed change adds complexity without meaningful security benefit.

**Assessment of each claim in the issue:**

1. **Session dependency / 30-minute timeout:** This is technically accurate but not a real problem. The OAuth flow with Philips Hue takes seconds, not minutes. A user clicks "Register Hub," is redirected to Hue, authorizes, and is immediately redirected back. The 30-minute session timeout is more than adequate. If a session somehow expires mid-flow, the user sees a clear error message asking them to retry — a perfectly acceptable UX outcome for an admin tool with a small user base.

2. **Multi-instance incompatibility:** This is speculative and acknowledged as such by the previous reviewer. The application runs as a single Docker container (`docker compose up`). The distributed memory cache is explicitly configured as in-memory (`AddDistributedMemoryCache()`). If the deployment model ever changes to multi-instance, session state would need to be addressed holistically (Redis, SQL-backed sessions, etc.) — not just for this one OAuth flow. Solving a hypothetical future problem in isolation is not productive.

3. **Session fixation risk:** This claim is vague and not substantiated. The session cookies are configured with `HttpOnly = true`, `SecurePolicy = CookieSecurePolicy.Always`, and `SameSite = SameSiteMode.Lax`. These are industry-standard hardening measures. Session fixation attacks require an attacker to set a victim's session cookie, which the `Secure` and `SameSite` flags mitigate. The CSRF token itself is a 128-bit GUID (`Guid.NewGuid().ToString("N")`), which provides 128 bits of entropy — well above OWASP's recommended minimum of 64 bits.

4. **OWASP A07 reference:** Citing A07:2021 (Identification and Authentication Failures) is a stretch. Session-based CSRF protection for OAuth state parameters is a well-established, OWASP-recommended pattern. The RFC 6749 specification (Section 10.12) recommends binding the state value to the user's session, which is exactly what this implementation does. The current code follows the specification faithfully.

**Why the current implementation is correct:**

- **State generation** (`Detail.cshtml.cs:OnPostRegisterHubAsync`): Generates a cryptographically random 128-bit GUID, stores it in session alongside the customer ID, and encodes both into the state parameter as `{customerId}:{csrfToken}`.
- **State validation** (`OAuthCallback.cshtml.cs:OnGetAsync`): Parses the state, checks that `expectedCsrf` is non-null (short-circuit on `expectedCsrf == null`), compares the CSRF token, validates the customer ID, and clears the session values after use. This is textbook OAuth state validation.
- **Session hardening** (`Program.cs`): Cookies are `HttpOnly`, `Secure`, `SameSite=Lax`, with a 30-minute idle timeout. The `[AllowAnonymous]` on the callback is necessary because the OAuth redirect comes from an external domain (api.meethue.com) and the user's auth cookie may not be sent in all browser configurations with strict cookie policies.

**Why signed state adds complexity without proportional benefit:**

- Requires introducing the Data Protection API dependency and key management (key rotation, key storage across restarts, key persistence in Docker volumes).
- Moves complexity from a well-understood session mechanism to a custom token format that must be carefully implemented to avoid introducing new vulnerabilities (e.g., replay attacks if the signed token is not bound to anything session-specific).
- The "embed authenticated user's identity" suggestion from the consolidated #0072 comment is contradicted by the `[AllowAnonymous]` requirement — there is no authenticated user identity available at callback time.
- For a single-instance admin portal with a handful of users, this is over-engineering.

**Regarding the first comment's claims:**

The previous critical reviewer already noted the null CSRF check claim is wrong — the code does check `expectedCsrf == null` before comparison. The suggestion to "embed the authenticated user's identity" to address the `[AllowAnonymous]` concern is self-contradictory: the callback must be `[AllowAnonymous]` precisely because authentication state may not be available during the OAuth redirect, so there is no user identity to embed.

**Recommendation:** Close this issue as Won't Fix. The current session-based CSRF protection follows RFC 6749 and OWASP guidelines, uses properly hardened session cookies, and is appropriate for the application's deployment model. If multi-instance deployment becomes a requirement in the future, session infrastructure should be addressed as a whole (e.g., switching to Redis-backed sessions), not by piecemeal changes to individual flows.
