# Issues

| ID | Title | Status | Priority | Labels |
|----|-------|--------|----------|--------|
| [0001](issues/0001-define-objectives-and-document-plan.md) | Define objectives and document plan | closed | medium | planning |
| [0002](issues/0002-add-initialization-to-issues-skill.md) | Add initialization procedure to issues skill | closed | medium | enhancement |
| [0003](issues/0003-setup-build-pipeline.md) | Setup build pipeline | closed | medium | planning |
| [0004](issues/0004-setup-hosting.md) | Setup hosting | closed | medium | planning |
| [0005](issues/0005-setup-ci-pipeline-for-dev-ci.md) | Setup CI pipeline for dev/ci | closed | medium | planning |
| [0006](issues/0006-setup-cd-pipeline-to-test-and-prod.md) | Setup CD pipeline to test and prod | closed | medium | planning |
| [0007](issues/0007-research-hue-api-docs.md) | Research API docs for Hue APIs and make available to Claude | closed | medium | documentation, planning |
| [0008](issues/0008-implementation-plan.md) | Implementation plan | closed | medium | planning, documentation |
| [0009](issues/0009-extract-shared-activity-summary-builder.md) | Extract shared ActivitySummaryBuilder to eliminate duplicated window aggregation logic | closed | high | enhancement, code-quality |
| [0010](issues/0010-introduce-status-readingtype-devicetype-constants.md) | Introduce string constants or enums for Status, ReadingType, and DeviceType fields | open | high | enhancement, code-quality |
| [0011](issues/0011-extract-device-reading-json-parsing.md) | Extract DeviceReading JSON parsing into typed accessor methods | closed | medium | enhancement, code-quality |
| [0012](issues/0012-extract-hub-apply-token-response.md) | Extract Hub.ApplyTokenResponse to eliminate 3-location token update duplication | open | medium | enhancement, code-quality |
| [0013](issues/0013-extract-load-customer-helper-detail-page.md) | Extract shared LoadCustomerAsync helper in Detail page model | open | low | enhancement, code-quality |
| [0014](issues/0014-extract-http-error-formatting-helper.md) | Extract HttpRequestException error formatting helper | open | low | enhancement, code-quality |
| [0015](issues/0015-replace-magic-numbers-with-constants.md) | Replace magic numbers and hardcoded color codes with named constants | open | medium | enhancement, code-quality |
| [0016](issues/0016-extract-shared-service-registration.md) | Extract shared DB/HttpClient bootstrap between Worker and Admin Program.cs | open | low | enhancement, code-quality |
| [0017](issues/0017-replace-batch-deletion-with-execute-delete.md) | Replace batch deletion with ExecuteDeleteAsync to avoid materializing rows | open | high | enhancement, performance |
| [0018](issues/0018-fix-unbounded-battery-history-query.md) | Fix unbounded battery history query in EmailRenderer | closed | high | bug, performance |
| [0019](issues/0019-batch-device-creation-polling.md) | Batch device creation in GetOrCreateDeviceAsync to reduce DB round-trips | open | medium | enhancement, performance |
| [0020](issues/0020-consolidate-email-renderer-queries.md) | Consolidate sequential ID-gathering queries in EmailRenderer | open | medium | enhancement, performance |
| [0021](issues/0021-collapse-sequential-count-queries.md) | Collapse sequential COUNT queries on Dashboard and About pages | open | low | enhancement, performance |
| [0022](issues/0022-add-polling-log-timestamp-index.md) | Add standalone Timestamp index on PollingLog for cleanup queries | open | medium | enhancement, performance |
| [0023](issues/0023-cache-parsed-send-times.md) | Cache parsed SendTimesUtc in EmailSchedulerService constructor | open | low | enhancement, performance |
| [0024](issues/0024-tokens-in-html-page-source.md) | Tokens embedded in HTML page source on Hub detail page | closed | high | security |
| [0025](issues/0025-logout-csrf-antiforgery.md) | Logout endpoint lacks antiforgery validation (CSRF logout) | open | medium | security |
| [0026](issues/0026-cookie-secure-policy-same-as-request.md) | Cookie SecurePolicy set to SameAsRequest allows HTTP transmission | closed | medium | security |
| [0027](issues/0027-rate-limiting-unbounded-dictionary.md) | Rate limiting dictionary has no size bounds (DoS risk) | open | medium | security |
| [0028](issues/0028-forwarded-headers-trust-all-proxies.md) | Forwarded headers trust all proxies — IP spoofing possible | open | low | security |
| [0029](issues/0029-cc-bcc-email-validation.md) | CC/BCC email fields lack server-side format validation | open | low | security |
| [0030](issues/0030-encrypt-tokens-at-rest.md) | Encrypt OAuth tokens at rest in SQLite database | open | critical | security |
| [0031](issues/0031-create-admin-tests-project.md) | Create Hpoll.Admin.Tests project for admin portal page models | closed | high | testing |
| [0032](issues/0032-expand-token-refresh-service-tests.md) | Expand TokenRefreshService tests (currently only 3 tests for 140 lines) | closed | high | testing |
| [0033](issues/0033-fix-timing-dependent-worker-tests.md) | Fix timing-dependent tests in Worker test suite | closed | high | testing, bug |
| [0034](issues/0034-add-email-renderer-edge-case-tests.md) | Add missing edge case tests for EmailRenderer | closed | medium | testing |
| [0035](issues/0035-add-dbcontext-model-tests.md) | Add DbContext model configuration tests (unique constraints, cascades) | closed | medium | testing |
| [0036](issues/0036-add-polling-service-advanced-tests.md) | Add PollingService multi-hub and batch cleanup tests | closed | medium | testing |
| [0037](issues/0037-improve-admin-code-coverage.md) | Improve code coverage for Hpoll.Admin (0% coverage) | closed | high | testing |
| [0038](issues/0038-add-xml-doc-comments.md) | Add XML documentation comments to public interfaces and services | open | high | documentation |
| [0039](issues/0039-add-architecture-overview-to-readme.md) | Add architecture overview and development instructions to README | open | medium | documentation |
| [0040](issues/0040-expand-env-example.md) | Expand .env.example to cover all configuration options | open | medium | documentation |
| [0041](issues/0041-add-inline-comments-complex-algorithms.md) | Add inline comments for complex algorithms and design decisions | open | low | documentation |
| [0042](issues/0042-add-security-response-headers.md) | Add security response headers (CSP, X-Frame-Options, HSTS) | open | high | security |
| [0043](issues/0043-hue-api-response-errors-not-checked.md) | Hue API response errors array is never checked after deserialization | open | medium | bug |
| [0044](issues/0044-silent-catch-blocks-in-json-parsing.md) | Silent catch blocks swallow JSON parsing exceptions without logging | closed | high | bug, code-quality |
| [0045](issues/0045-polling-401-no-immediate-token-refresh.md) | 401 during polling does not trigger immediate token refresh or status change | closed | high | bug |
| [0046](issues/0046-disabled-sensors-not-filtered-during-polling.md) | Disabled sensors are not filtered during polling | open | medium | bug |
| [0047](issues/0047-invalid-timezoneid-crashes-email-batch.md) | Invalid TimeZoneId crashes email sending for all subsequent customers | open | medium | bug |
| [0048](issues/0048-persist-data-protection-keys-secure-session-cookie.md) | Persist Data Protection keys and add Secure flag to session cookie | open | medium | security |
| [0049](issues/0049-health-evaluator-dead-code.md) | HealthEvaluator class is dead code — never registered or used | closed | low | code-quality |
| [0050](issues/0050-remove-orphaned-entrypoint-sh.md) | Remove or fix orphaned entrypoint.sh | closed | low | bug |
| [0051](issues/0051-battery-threshold-boundary-condition.md) | BatteryAlertThreshold boundary condition — device at exactly threshold level is neither shown nor alerted | open | medium | bug |
| [0052](issues/0052-pin-dependency-versions-reproducible-builds.md) | Pin NuGet and Docker dependency versions for reproducible builds | open | low | security, enhancement |
| [0053](issues/0053-register-app-leaks-response-body-in-exception.md) | RegisterApplicationAsync leaks full Hue API response body in exception message | closed | medium | bug, security |
| [0054](issues/0054-email-message-model-simplify-sender-interface.md) | Introduce EmailMessage model to simplify IEmailSender interface | open | medium | enhancement, code-quality |
| [0055](issues/0055-filter-device-readings-by-type-at-db-level.md) | Filter DeviceReadings by ReadingType at database level in activity summary queries | closed | medium | enhancement, performance |
| [0056](issues/0056-duplicate-json-parsing-motion-readings.md) | Duplicate JSON parsing of motion readings doubles memory and CPU cost | closed | medium | enhancement, performance |
| [0057](issues/0057-config-validation-tests-boundary-conditions.md) | Add configuration validation tests for boundary conditions | closed | medium | testing, bug |
| [0058](issues/0058-add-ci-coverage-thresholds-codecov-yml.md) | Add CI coverage thresholds and codecov.yml configuration | closed | medium | enhancement, testing |
| [0059](issues/0059-add-hueapiclient-error-path-tests.md) | Add missing HueApiClient error path tests | closed | medium | testing |
| [0060](issues/0060-consolidate-docker-ci-tag-building-script.md) | Consolidate Docker CI tag-building script into reusable action or shared script | open | low | enhancement |
| [0061](issues/0061-admin-portal-binds-all-interfaces.md) | Admin portal Docker container binds to all network interfaces | open | low | security |
| [0062](issues/0062-sequential-email-sending-limits-scalability.md) | Sequential email sending limits scalability for large customer counts | open | low | enhancement, performance |
| [0063](issues/0063-add-https-redirect-middleware-admin-portal.md) | Add HTTPS redirect middleware for admin portal in production | closed | medium | security |
| [0064](issues/0064-sanitize-exception-messages-in-polling-log.md) | Sanitize exception messages before persisting in PollingLog | open | medium | security |
| [0065](issues/0065-oauth-callback-signed-state-parameter.md) | OAuth callback should use signed state parameter instead of session-based CSRF | open | medium | security, enhancement |
| [0066](issues/0066-fix-claude-md-inaccuracies.md) | Fix CLAUDE.md inaccuracies: CI trigger pattern and missing battery mention | closed | low | documentation |
| [0067](issues/0067-entity-defaults-use-datetime-utcnow-at-instantiation.md) | Entity default values use DateTime.UtcNow at instantiation, not persistence time | open | medium | enhancement, code-quality |
| [0068](issues/0068-iemailrenderer-xml-doc-comment-misleading.md) | IEmailRenderer XML doc comment is misleading — hardcodes configurable values | closed | medium | documentation |
| [0069](issues/0069-polling-service-motion-comment-inaccurate.md) | PollingService motion detection comment is inaccurate per Hue API docs | closed | medium | documentation |
| [0070](issues/0070-devicereading-comments-missing-battery-type.md) | DeviceReading inline comments outdated — missing battery reading type | closed | medium | documentation |
| [0071](issues/0071-emailschedulerservice-executeasync-error-path-uncovered.md) | EmailSchedulerService.ExecuteAsync error retry path has 0% test coverage | open | medium | testing |
| [0072](issues/0072-oauthcallback-allowanonymous-sensitive-operations.md) | OAuthCallback page is AllowAnonymous but performs sensitive operations | closed | medium | security |
| [0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) | Missing CancellationToken propagation in Admin page handlers | open | low | enhancement, code-quality |
