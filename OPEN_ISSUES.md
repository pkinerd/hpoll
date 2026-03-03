# Open Issues Summary

*Last updated: 2026-03-03*

**16 open issues** | 2 medium | 14 low

## By Priority

### Medium

- [#0030](issues/0030-encrypt-tokens-at-rest.md) — Encrypt OAuth tokens at rest in SQLite database `security`
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted `enhancement` `code-quality`

### Low

- [#0048](issues/0048-persist-data-protection-keys-secure-session-cookie.md) — Persist Data Protection keys and add Secure flag to session cookie `security`
- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds `security` `enhancement`
- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers `enhancement` `code-quality`
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header `security` `enhancement`
- [#0086](issues/0086-hue-api-error-bodies-logged.md) — Hue API error response bodies logged with potential sensitive data `security`
- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation `enhancement` `performance`
- [#0121](issues/0121-admin-missing-exception-handler.md) — Admin portal lacks UseExceptionHandler middleware for production errors `enhancement`
- [#0128](issues/0128-dockerignore-missing-env-exclusion.md) — Dockerignore missing .env exclusion — defense-in-depth hardening `security`
- [#0129](issues/0129-systeminfoservice-datetime-utcnow.md) — SystemInfoService uses DateTime.UtcNow instead of TimeProvider `enhancement` `code-quality`
- [#0131](issues/0131-login-view-low-test-coverage.md) — Login.cshtml Razor view has uncovered rendering paths `testing`
- [#0132](issues/0132-pii-email-addresses-in-logs.md) — Email addresses logged in plaintext — compliance consideration `code-quality`
- [#0133](issues/0133-unused-device-roomname-property.md) — Device.RoomName property is never populated during polling `code-quality`
- [#0134](issues/0134-admin-dashboard-hardcoded-token-threshold.md) — Admin dashboard hardcodes 48-hour token expiry threshold `code-quality`
- [#0135](issues/0135-readme-missing-admin-password-hash.md) — README settings table does not include non-.NET env vars like ADMIN_PASSWORD_HASH `documentation`

## By Label

### security

- [#0030](issues/0030-encrypt-tokens-at-rest.md) — Encrypt OAuth tokens at rest in SQLite database (medium)
- [#0048](issues/0048-persist-data-protection-keys-secure-session-cookie.md) — Persist Data Protection keys and add Secure flag to session cookie (low)
- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds (low)
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header (low)
- [#0086](issues/0086-hue-api-error-bodies-logged.md) — Hue API error response bodies logged with potential sensitive data (low)
- [#0128](issues/0128-dockerignore-missing-env-exclusion.md) — Dockerignore missing .env exclusion — defense-in-depth hardening (low)

### enhancement

- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds (low)
- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers (low)
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header (low)
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted (medium)
- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation (low)
- [#0121](issues/0121-admin-missing-exception-handler.md) — Admin portal lacks UseExceptionHandler middleware for production errors (low)
- [#0129](issues/0129-systeminfoservice-datetime-utcnow.md) — SystemInfoService uses DateTime.UtcNow instead of TimeProvider (low)

### code-quality

- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers (low)
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted (medium)
- [#0129](issues/0129-systeminfoservice-datetime-utcnow.md) — SystemInfoService uses DateTime.UtcNow instead of TimeProvider (low)
- [#0132](issues/0132-pii-email-addresses-in-logs.md) — Email addresses logged in plaintext — compliance consideration (low)
- [#0133](issues/0133-unused-device-roomname-property.md) — Device.RoomName property is never populated during polling (low)
- [#0134](issues/0134-admin-dashboard-hardcoded-token-threshold.md) — Admin dashboard hardcodes 48-hour token expiry threshold (low)

### testing

- [#0131](issues/0131-login-view-low-test-coverage.md) — Login.cshtml Razor view has uncovered rendering paths (low)

### performance

- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation (low)

### documentation

- [#0135](issues/0135-readme-missing-admin-password-hash.md) — README settings table does not include non-.NET env vars like ADMIN_PASSWORD_HASH (low)
