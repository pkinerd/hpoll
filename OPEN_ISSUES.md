# Open Issues Summary

*Last updated: 2026-03-15*

**11 open issues** | 3 medium | 8 low

## By Priority

### Medium

- [#0030](issues/0030-encrypt-tokens-at-rest.md) — Encrypt OAuth tokens at rest in SQLite database `security`
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted `enhancement` `code-quality`
- [#0148](issues/0148-send-email-now-button.md) — Add 'Send Email Now' button to Customer Detail page `feature` `enhancement`

### Low
- [#0048](issues/0048-persist-data-protection-keys-secure-session-cookie.md) — Persist Data Protection keys and add Secure flag to session cookie `security`
- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds `security` `enhancement`
- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers `enhancement` `code-quality`
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header `security` `enhancement`
- [#0086](issues/0086-hue-api-error-bodies-logged.md) — Hue API error response bodies logged with potential sensitive data `security`
- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation `enhancement` `performance`
- [#0121](issues/0121-admin-missing-exception-handler.md) — Admin portal lacks UseExceptionHandler middleware for production errors `enhancement`
- [#0146](issues/0146-getorcreatedeviceasync-untested.md) — GetOrCreateDeviceAsync edge case (null/empty HueDeviceId) not tested `testing`
- [#0147](issues/0147-hueapiclient-cancellationtoken-untested.md) — HueApiClient CancellationToken propagation untested `testing`

## By Label

### code-quality

- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers (low)
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted (medium)

### enhancement

- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds (low)
- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers (low)
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header (low)
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted (medium)
- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation (low)
- [#0121](issues/0121-admin-missing-exception-handler.md) — Admin portal lacks UseExceptionHandler middleware for production errors (low)
- [#0148](issues/0148-send-email-now-button.md) — Add 'Send Email Now' button to Customer Detail page (medium)

### feature

- [#0148](issues/0148-send-email-now-button.md) — Add 'Send Email Now' button to Customer Detail page (medium)

### performance

- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation (low)

### security

- [#0030](issues/0030-encrypt-tokens-at-rest.md) — Encrypt OAuth tokens at rest in SQLite database (medium)
- [#0048](issues/0048-persist-data-protection-keys-secure-session-cookie.md) — Persist Data Protection keys and add Secure flag to session cookie (low)
- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds (low)
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header (low)
- [#0086](issues/0086-hue-api-error-bodies-logged.md) — Hue API error response bodies logged with potential sensitive data (low)

### testing

- [#0146](issues/0146-getorcreatedeviceasync-untested.md) — GetOrCreateDeviceAsync edge case (null/empty HueDeviceId) not tested (low)
- [#0147](issues/0147-hueapiclient-cancellationtoken-untested.md) — HueApiClient CancellationToken propagation untested (low)
