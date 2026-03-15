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
| [0010](issues/0010-introduce-status-readingtype-devicetype-constants.md) | Introduce string constants or enums for Status, ReadingType, and DeviceType fields | closed | medium | enhancement, code-quality |
| [0011](issues/0011-extract-device-reading-json-parsing.md) | Extract DeviceReading JSON parsing into typed accessor methods | closed | medium | enhancement, code-quality |
| [0012](issues/0012-extract-hub-apply-token-response.md) | Extract Hub.ApplyTokenResponse to eliminate 3-location token update duplication | closed | medium | enhancement, code-quality |
| [0013](issues/0013-extract-load-customer-helper-detail-page.md) | Extract shared LoadCustomerAsync helper in Detail page model | closed | low | enhancement, code-quality |
| [0014](issues/0014-extract-http-error-formatting-helper.md) | Extract HttpRequestException error formatting helper | closed | low | enhancement, code-quality |
| [0015](issues/0015-replace-magic-numbers-with-constants.md) | Replace magic numbers and hardcoded color codes with named constants | closed | low | enhancement, code-quality |
| [0016](issues/0016-extract-shared-service-registration.md) | Extract shared DB/HttpClient bootstrap between Worker and Admin Program.cs | closed | low | enhancement, code-quality |
| [0017](issues/0017-replace-batch-deletion-with-execute-delete.md) | Replace batch deletion with ExecuteDeleteAsync to avoid materializing rows | closed | high | enhancement, performance |
| [0018](issues/0018-fix-unbounded-battery-history-query.md) | Fix unbounded battery history query in EmailRenderer | closed | high | bug, performance |
| [0019](issues/0019-batch-device-creation-polling.md) | Batch device creation in GetOrCreateDeviceAsync to reduce DB round-trips | closed | medium | enhancement, performance |
| [0020](issues/0020-consolidate-email-renderer-queries.md) | Consolidate sequential ID-gathering queries in EmailRenderer | closed | medium | enhancement, performance |
| [0021](issues/0021-collapse-sequential-count-queries.md) | Collapse sequential COUNT queries on Dashboard and About pages | closed | low | enhancement, performance |
| [0022](issues/0022-add-polling-log-timestamp-index.md) | Add standalone Timestamp index on PollingLog for cleanup queries | closed | medium | enhancement, performance |
| [0023](issues/0023-cache-parsed-send-times.md) | Cache parsed SendTimesUtc in EmailSchedulerService constructor | closed | low | enhancement, performance |
| [0024](issues/0024-tokens-in-html-page-source.md) | Tokens embedded in HTML page source on Hub detail page | closed | high | security |
| [0025](issues/0025-logout-csrf-antiforgery.md) | Logout endpoint lacks antiforgery validation (CSRF logout) | closed | low | security |
| [0026](issues/0026-cookie-secure-policy-same-as-request.md) | Cookie SecurePolicy set to SameAsRequest allows HTTP transmission | closed | medium | security |
| [0027](issues/0027-rate-limiting-unbounded-dictionary.md) | Rate limiting dictionary has no size bounds (DoS risk) | closed | low | security |
| [0028](issues/0028-forwarded-headers-trust-all-proxies.md) | Forwarded headers trust all proxies — IP spoofing possible | closed | low | security |
| [0029](issues/0029-cc-bcc-email-validation.md) | CC/BCC email fields lack server-side format validation | closed | low | security |
| [0030](issues/0030-encrypt-tokens-at-rest.md) | Encrypt OAuth tokens at rest in SQLite database | open | medium | security |
| [0031](issues/0031-create-admin-tests-project.md) | Create Hpoll.Admin.Tests project for admin portal page models | closed | high | testing |
| [0032](issues/0032-expand-token-refresh-service-tests.md) | Expand TokenRefreshService tests (currently only 3 tests for 140 lines) | closed | high | testing |
| [0033](issues/0033-fix-timing-dependent-worker-tests.md) | Fix timing-dependent tests in Worker test suite | closed | high | testing, bug |
| [0034](issues/0034-add-email-renderer-edge-case-tests.md) | Add missing edge case tests for EmailRenderer | closed | medium | testing |
| [0035](issues/0035-add-dbcontext-model-tests.md) | Add DbContext model configuration tests (unique constraints, cascades) | closed | medium | testing |
| [0036](issues/0036-add-polling-service-advanced-tests.md) | Add PollingService multi-hub and batch cleanup tests | closed | medium | testing |
| [0037](issues/0037-improve-admin-code-coverage.md) | Improve code coverage for Hpoll.Admin (0% coverage) | closed | high | testing |
| [0038](issues/0038-add-xml-doc-comments.md) | Add XML documentation comments to public interfaces and services | closed | medium | documentation |
| [0039](issues/0039-add-architecture-overview-to-readme.md) | Add architecture overview and development instructions to README | closed | low | documentation |
| [0040](issues/0040-expand-env-example.md) | Expand .env.example to cover all configuration options | closed | low | documentation |
| [0041](issues/0041-add-inline-comments-complex-algorithms.md) | Add inline comments for complex algorithms and design decisions | closed | low | documentation |
| [0042](issues/0042-add-security-response-headers.md) | Add security response headers (CSP, X-Frame-Options, HSTS) | closed | medium | security |
| [0043](issues/0043-hue-api-response-errors-not-checked.md) | Hue API response errors array is never checked after deserialization | closed | low | bug |
| [0044](issues/0044-silent-catch-blocks-in-json-parsing.md) | Silent catch blocks swallow JSON parsing exceptions without logging | closed | high | bug, code-quality |
| [0045](issues/0045-polling-401-no-immediate-token-refresh.md) | 401 during polling does not trigger immediate token refresh or status change | closed | high | bug |
| [0046](issues/0046-disabled-sensors-not-filtered-during-polling.md) | Disabled sensors are not filtered during polling | closed | medium | bug |
| [0047](issues/0047-invalid-timezoneid-crashes-email-batch.md) | Invalid TimeZoneId crashes email sending for all subsequent customers | closed | medium | bug |
| [0048](issues/0048-persist-data-protection-keys-secure-session-cookie.md) | Persist Data Protection keys and add Secure flag to session cookie | open | low | security |
| [0049](issues/0049-health-evaluator-dead-code.md) | HealthEvaluator class is dead code — never registered or used | closed | low | code-quality |
| [0050](issues/0050-remove-orphaned-entrypoint-sh.md) | Remove or fix orphaned entrypoint.sh | closed | low | bug |
| [0051](issues/0051-battery-threshold-boundary-condition.md) | BatteryAlertThreshold boundary condition — device at exactly threshold level is neither shown nor alerted | closed | medium | bug |
| [0052](issues/0052-pin-dependency-versions-reproducible-builds.md) | Pin NuGet and Docker dependency versions for reproducible builds | open | low | security, enhancement |
| [0053](issues/0053-register-app-leaks-response-body-in-exception.md) | RegisterApplicationAsync leaks full Hue API response body in exception message | closed | medium | bug, security |
| [0054](issues/0054-email-message-model-simplify-sender-interface.md) | Introduce EmailMessage model to simplify IEmailSender interface | closed | low | enhancement, code-quality |
| [0055](issues/0055-filter-device-readings-by-type-at-db-level.md) | Filter DeviceReadings by ReadingType at database level in activity summary queries | closed | medium | enhancement, performance |
| [0056](issues/0056-duplicate-json-parsing-motion-readings.md) | Duplicate JSON parsing of motion readings doubles memory and CPU cost | closed | medium | enhancement, performance |
| [0057](issues/0057-config-validation-tests-boundary-conditions.md) | Add configuration validation tests for boundary conditions | closed | medium | testing, bug |
| [0058](issues/0058-add-ci-coverage-thresholds-codecov-yml.md) | Add CI coverage thresholds and codecov.yml configuration | closed | medium | enhancement, testing |
| [0059](issues/0059-add-hueapiclient-error-path-tests.md) | Add missing HueApiClient error path tests | closed | medium | testing |
| [0060](issues/0060-consolidate-docker-ci-tag-building-script.md) | Consolidate Docker CI tag-building script into reusable action or shared script | closed | low | enhancement |
| [0061](issues/0061-admin-portal-binds-all-interfaces.md) | Admin portal Docker container binds to all network interfaces | closed | low | security |
| [0062](issues/0062-sequential-email-sending-limits-scalability.md) | Sequential email sending limits scalability for large customer counts | closed | low | enhancement, performance |
| [0063](issues/0063-add-https-redirect-middleware-admin-portal.md) | Add HTTPS redirect middleware for admin portal in production | closed | medium | security |
| [0064](issues/0064-sanitize-exception-messages-in-polling-log.md) | Sanitize exception messages before persisting in PollingLog | closed | low | security |
| [0065](issues/0065-oauth-callback-signed-state-parameter.md) | OAuth callback should use signed state parameter instead of session-based CSRF | closed | low | security, enhancement |
| [0066](issues/0066-fix-claude-md-inaccuracies.md) | Fix CLAUDE.md inaccuracies: CI trigger pattern and missing battery mention | closed | low | documentation |
| [0067](issues/0067-entity-defaults-use-datetime-utcnow-at-instantiation.md) | Entity default values use DateTime.UtcNow at instantiation, not persistence time | closed | low | enhancement, code-quality |
| [0068](issues/0068-iemailrenderer-xml-doc-comment-misleading.md) | IEmailRenderer XML doc comment is misleading — hardcodes configurable values | closed | medium | documentation |
| [0069](issues/0069-polling-service-motion-comment-inaccurate.md) | PollingService motion detection comment is inaccurate per Hue API docs | closed | medium | documentation |
| [0070](issues/0070-devicereading-comments-missing-battery-type.md) | DeviceReading inline comments outdated — missing battery reading type | closed | medium | documentation |
| [0071](issues/0071-emailschedulerservice-executeasync-error-path-uncovered.md) | EmailSchedulerService.ExecuteAsync error retry path has 0% test coverage | closed | low | testing |
| [0072](issues/0072-oauthcallback-allowanonymous-sensitive-operations.md) | OAuthCallback page is AllowAnonymous but performs sensitive operations | closed | medium | security |
| [0073](issues/0073-missing-cancellationtoken-in-admin-handlers.md) | Missing CancellationToken propagation in Admin page handlers | open | low | enhancement, code-quality |
| [0074](issues/0074-refactor-inline-js-css-and-add-csp.md) | Refactor inline JS/CSS out of Razor pages and add Content-Security-Policy header | open | low | security, enhancement |
| [0075](issues/0075-activity-window-duplication-persists.md) | Activity window duplication persists — ActivitySummaryBuilder never extracted | open | medium | enhancement, code-quality |
| [0076](issues/0076-inconsistent-timeprovider-usage.md) | Inconsistent TimeProvider vs DateTime.UtcNow usage across codebase | closed | low | enhancement, code-quality |
| [0077](issues/0077-oauthcallback-razor-view-zero-coverage.md) | OAuthCallback Razor view has 0% test coverage | closed | low | testing |
| [0078](issues/0078-xml-doc-gaps-worker-services-config.md) | XML doc gaps remain on Worker services and configuration classes | closed | low | documentation |
| [0079](issues/0079-vacuum-into-sql-injection-risk.md) | VACUUM INTO uses string interpolation creating SQL injection risk | closed | low | security |
| [0080](issues/0080-claude-md-project-structure-inaccuracies.md) | CLAUDE.md project structure has multiple inaccuracies | closed | medium | documentation |
| [0081](issues/0081-login-rate-limiting-lockout-untested.md) | Login rate-limiting lockout path is untested (security-critical) | closed | high | testing, security |
| [0082](issues/0082-register-hub-handler-uncovered.md) | OnPostRegisterHubAsync OAuth handler entirely uncovered by tests | closed | medium | testing, security |
| [0083](issues/0083-misleading-motion-report-changed-comment.md) | Misleading HueMotionReport.Changed XML doc comment | closed | medium | documentation |
| [0084](issues/0084-loginmodel-tests-global-env-isolation.md) | LoginModel tests use global Environment creating test isolation risk | closed | medium | testing, code-quality |
| [0085](issues/0085-cookie-auth-no-absolute-expiration.md) | Cookie authentication lacks absolute session expiration | closed | low | security |
| [0086](issues/0086-hue-api-error-bodies-logged.md) | Hue API error response bodies logged with potential sensitive data | open | low | security |
| [0087](issues/0087-pollingservice-executeasync-loop-untested.md) | PollingService.ExecuteAsync main service loop has no test coverage | closed | medium | testing |
| [0088](issues/0088-pollingservicetests-createdb-leaks-scopes.md) | PollingServiceTests.CreateDb() leaks service scopes | closed | low | testing, code-quality |
| [0090](issues/0090-integration-tests-share-database-state.md) | Integration tests share intra-class database state creating fragile tests | closed | medium | testing, code-quality |
| [0091](issues/0091-databasebackupservice-error-paths-untested.md) | DatabaseBackupService error handling paths untested | closed | low | testing |
| [0094](issues/0094-oauth-registration-flow-lacks-architectural-docs.md) | OAuth hub registration flow lacks architectural documentation | closed | low | documentation |
| [0095](issues/0095-readme-phantom-settings-and-duplicate-header.md) | README references nonexistent PollingSettings and has duplicate section header | closed | medium | documentation |
| [0096](issues/0096-token-reveal-cacheable-get.md) | Token reveal endpoint uses cacheable GET request | closed | low | security |
| [0097](issues/0097-setup-mode-hash-generator-exposed.md) | Setup mode hash generator lacks server-side guard when password already configured | closed | low | enhancement |
| [0098](issues/0098-clearallasync-test-wrong-behavior.md) | SystemInfoService.ClearAllAsync test verifies exception instead of actual behavior | closed | low | testing, code-quality |
| [0099](issues/0099-emailscheduler-renderer-failure-untested.md) | EmailSchedulerService lacks test for renderer failure path | closed | low | testing |
| [0100](issues/0100-exchange-auth-code-error-untested.md) | ExchangeAuthorizationCodeAsync error path has no test coverage | closed | low | testing |
| [0102](issues/0102-tokenrefresh-executeasync-untested.md) | TokenRefreshService.ExecuteAsync outer exception catch untested | closed | low | testing |
| [0103](issues/0103-tokenrefresh-boundary-test-flaky.md) | TokenRefreshService boundary test has overly weak assertion | closed | low | testing, code-quality |
| [0104](issues/0104-customer-detail-error-success-styling.md) | Customer Detail error message displayed with success styling | closed | low | bug |
| [0105](issues/0105-email-renderer-incomplete-html-encoding.md) | Timezone display name not HTML-encoded in email renderer | closed | low | security |
| [0106](issues/0106-hueapiclient-intermediate-string-deserialization.md) | HueApiClient deserializes JSON via intermediate string allocation | open | low | enhancement, performance |
| [0107](issues/0107-backup-test-task-delay-timing.md) | DatabaseBackupService test uses Task.Delay for timestamp separation | closed | low | testing, code-quality |
| [0108](issues/0108-unused-customers-config-appsettings.md) | Unused Customers configuration key in appsettings.json | closed | low | code-quality |
| [0109](issues/0109-emailrenderer-dbcontext-tight-coupling.md) | EmailRenderer directly references DbContext rather than abstraction | closed | low | enhancement, code-quality |
| [0110](issues/0110-background-service-loop-duplication.md) | SystemInfo metric update pattern duplicated across 3 background services | closed | low | enhancement, code-quality |
| [0111](issues/0111-devicereading-readingtype-composite-index.md) | Consider composite index on DeviceReading for ReadingType filtering | closed | low | enhancement, performance |
| [0112](issues/0112-backup-counter-drift-after-pruning.md) | DatabaseBackupService total backups counter produces meaningless hybrid value | closed | low | bug, code-quality |
| [0114](issues/0114-email-data-tests-wrong-project.md) | Email and Data tests placed in unrelated test projects | closed | low | testing, code-quality |
| [0116](issues/0116-test-polling-motion-cutoff-extended-downtime.md) | Add test for PollingService motion cutoff after extended downtime | closed | medium | testing |
| [0120](issues/0120-admin-missing-asnotracking.md) | Missing AsNoTracking on read-only Admin queries wastes change-tracker memory | closed | low | enhancement, performance |
| [0121](issues/0121-admin-missing-exception-handler.md) | Admin portal lacks UseExceptionHandler middleware for production errors | open | low | enhancement |
| [0122](issues/0122-about-page-info-leakage.md) | About page exposes detailed system and deployment information | closed | low | security |
| [0123](issues/0123-email-window-bucketing-undocumented.md) | Extract 60-minute incomplete-window threshold into named constant | closed | low | documentation |
| [0126](issues/0126-readme-missing-backup-docs.md) | README missing Backup configuration documentation | closed | low | documentation |
| [0127](issues/0127-customer-create-send-time-wrong-default.md) | Customer create form shows wrong send time default and doesn't allow blank for system default | closed | medium | bug |
| [0128](issues/0128-dockerignore-missing-env-exclusion.md) | Dockerignore missing .env exclusion — defense-in-depth hardening | closed | low | security |
| [0129](issues/0129-systeminfoservice-datetime-utcnow.md) | SystemInfoService uses DateTime.UtcNow instead of TimeProvider | closed | low | enhancement, code-quality |
| [0131](issues/0131-login-view-low-test-coverage.md) | Login.cshtml Razor view has uncovered rendering paths | closed | low | testing |
| [0132](issues/0132-pii-email-addresses-in-logs.md) | Email addresses logged in plaintext — compliance consideration | closed | low | code-quality |
| [0133](issues/0133-unused-device-roomname-property.md) | Device.RoomName property is never populated during polling | closed | low | code-quality |
| [0134](issues/0134-admin-dashboard-hardcoded-token-threshold.md) | Admin dashboard hardcodes 48-hour token expiry threshold | closed | low | code-quality |
| [0135](issues/0135-readme-missing-admin-password-hash.md) | README settings table does not include non-.NET env vars like ADMIN_PASSWORD_HASH | closed | low | documentation |
| [0136](issues/0136-duplicated-maskemail-utility.md) | Duplicated MaskEmail utility across Worker and Email projects | closed | medium | enhancement, code-quality |
| [0137](issues/0137-duplicated-geteffectivedefaultsendtimesutcasync.md) | Duplicated GetEffectiveDefaultSendTimesUtcAsync in Create and Detail pages | closed | medium | enhancement, code-quality |
| [0138](issues/0138-admin-pages-datetime-utcnow-not-testable.md) | Admin page models use DateTime.UtcNow instead of TimeProvider | closed | low | enhancement, code-quality, testing |
| [0139](issues/0139-xml-doc-gaps-config-entity-pagemodel.md) | XML doc comment gaps on configuration, entity, and page model classes | closed | low | documentation |
| [0140](issues/0140-backgroundservice-executeasync-untested.md) | PollingService.ExecuteAsync has no test coverage; other services have partial error-path gaps | closed | medium | testing |
| [0142](issues/0142-maskemail-zero-test-coverage.md) | MaskEmail methods have zero test coverage | closed | low | testing |
| [0143](issues/0143-readme-missing-battery-in-intro.md) | README introduction omits battery sensor data | closed | low | documentation |
| [0144](issues/0144-oauthcallback-manual-token-mapping.md) | OAuthCallbackModel manually maps tokens instead of using ApplyTokenResponse | closed | low | enhancement, code-quality |
| [0145](issues/0145-repetitive-customer-loading-detail-page.md) | Repetitive customer-loading boilerplate in Detail page handlers | closed | low | enhancement, code-quality |
| [0146](issues/0146-getorcreatedeviceasync-untested.md) | GetOrCreateDeviceAsync edge case (null/empty HueDeviceId) not tested | closed | low | testing |
| [0147](issues/0147-hueapiclient-cancellationtoken-untested.md) | HueApiClient CancellationToken propagation untested | closed | low | testing |
| [0148](issues/0148-send-email-now-button.md) | Add 'Send Email Now' button to Customer Detail page | open | medium | feature, enhancement |
| [0150](issues/0150-backup-tests-cwd-mutation.md) | DatabaseBackupServiceTests mutates process-wide CWD creating test isolation risk | closed | low | testing, code-quality |
| [0152](issues/0152-adminsettings-passwordhash-doc-inaccurate.md) | AdminSettings.PasswordHash doc comment says 'Null' but code checks IsNullOrEmpty | closed | low | documentation |
| [0156](issues/0156-emailscheduler-tests-datetime-utcnow-flaky.md) | EmailSchedulerServiceTests use DateTime.UtcNow instead of mock TimeProvider | closed | low | testing, code-quality |
| [0157](issues/0157-polling-finally-save-failure-untested.md) | PollingService finally-block SaveChangesAsync failure path untested | closed | low | testing |
| [0159](issues/0159-emailrenderer-buildhtml-monolith.md) | EmailRenderer.BuildHtml is a 120-line monolith with inline HTML construction | closed | low | enhancement, code-quality |
| [0160](issues/0160-sendtimedisplayservice-no-tests.md) | SendTimeDisplayService has no unit tests | closed | low | testing |
| [0162](issues/0162-tokenrefresh-tests-no-faketimeprovider.md) | TokenRefreshServiceTests use DateTime.UtcNow instead of FakeTimeProvider | closed | low | testing, code-quality |
| [0163](issues/0163-readme-pbkdf2-sha256-incorrect.md) | README incorrectly states password hashing uses PBKDF2-SHA256 (actual: SHA512) | closed | medium | documentation |
| [0165](issues/0165-pollingservicetests-missing-faketimeprovider-injection.md) | PollingServiceTests doesn't inject FakeTimeProvider by default | closed | low | testing, code-quality |
| [0166](issues/0166-emailscheduler-setasync-not-batched.md) | EmailSchedulerService uses two SetAsync calls instead of SetBatchAsync | closed | low | enhancement, code-quality |
| [0167](issues/0167-oauth-error-param-reflected.md) | OAuth callback reflects error query parameter on AllowAnonymous page | closed | low | security |
| [0169](issues/0169-adminsettings-passwordhash-says-bcrypt.md) | AdminSettings.PasswordHash XML doc incorrectly says BCrypt | closed | low | documentation |
| [0171](issues/0171-oauth-callback-no-partial-persistence.md) | OAuthCallback multi-step registration loses tokens on partial failure | closed | low | enhancement, code-quality |
| [0172](issues/0172-hub-detail-generic-exception-paths-untested.md) | Hub Detail generic Exception and null-StatusCode error paths untested | closed | low | testing |
| [0173](issues/0173-customer-detail-updateemails-notfound-untested.md) | Customer Detail POST handlers missing invalid-customer NotFound tests | closed | low | testing |
| [0174](issues/0174-sendtimedisplayservice-queries-db-every-page-load.md) | SendTimeDisplayService queries database on every page load | closed | low | enhancement, performance |
| [0179](issues/0179-oauthcallback-error-switch-branches-untested.md) | OAuthCallback error mapping switch branches mostly untested | open | low | testing |
