# Open Issues Summary

*Last updated: 2026-03-30*

**16 open issues** | 1 high | 2 medium | 13 low

## By Priority

### High

- [#0190](issues/0190-dst-crash-activity-summary-email-renderer.md) — DST spring-forward gap crash in activity summary and email renderer `bug`

### Medium

- [#0030](issues/0030-encrypt-tokens-at-rest.md) — Encrypt OAuth tokens at rest in SQLite database `security`
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted `enhancement` `code-quality`
- [#0192](issues/0192-zigbee-status-enum-values-incorrect.md) — HueZigbeeConnectivityResource.Status enum values incorrect per API spec `bug` `documentation`

### Low

- [#0048](issues/0048-persist-data-protection-keys-secure-session-cookie.md) — Persist Data Protection keys and add Secure flag to session cookie `security`
- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds `security` `enhancement`
- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers `enhancement` `code-quality`
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header `security` `enhancement`
- [#0086](issues/0086-hue-api-error-bodies-logged.md) — Hue API error response bodies logged with potential sensitive data `security`
- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation `enhancement` `performance`
- [#0121](issues/0121-admin-missing-exception-handler.md) — Admin portal lacks UseExceptionHandler middleware for production errors `enhancement`
- [#0182](issues/0182-emailmasker-mask-null-nre.md) — EmailMasker.Mask(null) lacks null guard `enhancement` `code-quality`
- [#0184](issues/0184-hueapiclient-malformed-json-200.md) — HueApiClient logs unclear error on malformed JSON 200 response `enhancement` `code-quality`
- [#0187](issues/0187-systeminfoextensions-trysetbatch-exception-uncovered.md) — SystemInfoServiceExtensions.TrySetBatchAsync OperationCanceledException path uncovered `testing`
- [#0191](issues/0191-devicereading-value-comment-zigbee-motion.md) — DeviceReading.Value comment missing zigbee_connectivity and misleading motion semantics `documentation`
- [#0193](issues/0193-mac-addresses-cleartext-sanitized-export.md) — MAC addresses stored cleartext in DeviceReadings and included in sanitized export `security`

## By Label

### bug

- [#0190](issues/0190-dst-crash-activity-summary-email-renderer.md) — DST spring-forward gap crash in activity summary and email renderer (high)
- [#0192](issues/0192-zigbee-status-enum-values-incorrect.md) — HueZigbeeConnectivityResource.Status enum values incorrect per API spec (medium)

### code-quality

- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers (low)
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted (medium)
- [#0182](issues/0182-emailmasker-mask-null-nre.md) — EmailMasker.Mask(null) lacks null guard (low)
- [#0184](issues/0184-hueapiclient-malformed-json-200.md) — HueApiClient logs unclear error on malformed JSON 200 response (low)

### documentation

- [#0191](issues/0191-devicereading-value-comment-zigbee-motion.md) — DeviceReading.Value comment missing zigbee_connectivity and misleading motion semantics (low)
- [#0192](issues/0192-zigbee-status-enum-values-incorrect.md) — HueZigbeeConnectivityResource.Status enum values incorrect per API spec (medium)

### enhancement

- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds (low)
- [#0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) — Missing CancellationToken propagation in Admin page handlers (low)
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header (low)
- [#0075](issues/0075-activity-window-duplication-persists.md) — Activity window duplication persists — ActivitySummaryBuilder never extracted (medium)
- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation (low)
- [#0121](issues/0121-admin-missing-exception-handler.md) — Admin portal lacks UseExceptionHandler middleware for production errors (low)
- [#0182](issues/0182-emailmasker-mask-null-nre.md) — EmailMasker.Mask(null) lacks null guard (low)
- [#0184](issues/0184-hueapiclient-malformed-json-200.md) — HueApiClient logs unclear error on malformed JSON 200 response (low)

### performance

- [#0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) — HueApiClient deserializes JSON via intermediate string allocation (low)

### security

- [#0030](issues/0030-encrypt-tokens-at-rest.md) — Encrypt OAuth tokens at rest in SQLite database (medium)
- [#0048](issues/0048-persist-data-protection-keys-secure-session-cookie.md) — Persist Data Protection keys and add Secure flag to session cookie (low)
- [#0052](issues/0052-pin-dependency-versions-reproducible-builds.md) — Pin NuGet and Docker dependency versions for reproducible builds (low)
- [#0074](issues/0074-refactor-inline-js-css-and-add-csp.md) — Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header (low)
- [#0086](issues/0086-hue-api-error-bodies-logged.md) — Hue API error response bodies logged with potential sensitive data (low)
- [#0193](issues/0193-mac-addresses-cleartext-sanitized-export.md) — MAC addresses stored cleartext in DeviceReadings and included in sanitized export (low)

### testing

- [#0187](issues/0187-systeminfoextensions-trysetbatch-exception-uncovered.md) — SystemInfoServiceExtensions.TrySetBatchAsync OperationCanceledException path uncovered (low)
