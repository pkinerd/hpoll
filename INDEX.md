# Issues

| ID | Title | Status | Priority | Labels |
|----|-------|--------|----------|--------|
| 0001 | Define objectives and document plan | closed | medium | planning |
| 0002 | Add initialization procedure to issues skill | closed | medium | enhancement |
| 0003 | Setup build pipeline | closed | medium | planning |
| 0004 | Setup hosting | closed | medium | planning |
| 0005 | Setup CI pipeline for dev/ci | closed | medium | planning |
| 0006 | Setup CD pipeline to test and prod | closed | medium | planning |
| 0007 | Research API docs for Hue APIs and make available to Claude | closed | medium | documentation, planning |
| 0008 | Implementation plan | closed | medium | planning, documentation |
| 0009 | Extract shared ActivitySummaryBuilder to eliminate duplicated window aggregation logic | open | high | enhancement, code-quality |
| 0010 | Introduce string constants or enums for Status, ReadingType, and DeviceType fields | open | high | enhancement, code-quality |
| 0011 | Extract DeviceReading JSON parsing into typed accessor methods | open | medium | enhancement, code-quality |
| 0012 | Extract Hub.ApplyTokenResponse to eliminate 3-location token update duplication | open | medium | enhancement, code-quality |
| 0013 | Extract shared LoadCustomerAsync helper in Detail page model | open | low | enhancement, code-quality |
| 0014 | Extract HttpRequestException error formatting helper | open | low | enhancement, code-quality |
| 0015 | Replace magic numbers and hardcoded color codes with named constants | open | medium | enhancement, code-quality |
| 0016 | Extract shared DB/HttpClient bootstrap between Worker and Admin Program.cs | open | low | enhancement, code-quality |
| 0017 | Replace batch deletion with ExecuteDeleteAsync to avoid materializing rows | open | high | enhancement, performance |
| 0018 | Fix unbounded battery history query in EmailRenderer | open | high | bug, performance |
| 0019 | Batch device creation in GetOrCreateDeviceAsync to reduce DB round-trips | open | medium | enhancement, performance |
| 0020 | Consolidate sequential ID-gathering queries in EmailRenderer | open | medium | enhancement, performance |
| 0021 | Collapse sequential COUNT queries on Dashboard and About pages | open | low | enhancement, performance |
| 0022 | Add standalone Timestamp index on PollingLog for cleanup queries | open | medium | enhancement, performance |
| 0023 | Cache parsed SendTimesUtc in EmailSchedulerService constructor | open | low | enhancement, performance |
| 0024 | Tokens embedded in HTML page source on Hub detail page | open | high | security |
| 0025 | Logout endpoint lacks antiforgery validation (CSRF logout) | open | medium | security |
| 0026 | Cookie SecurePolicy set to SameAsRequest allows HTTP transmission | open | medium | security |
| 0027 | Rate limiting dictionary has no size bounds (DoS risk) | open | medium | security |
| 0028 | Forwarded headers trust all proxies — IP spoofing possible | open | low | security |
| 0029 | CC/BCC email fields lack server-side format validation | open | low | security |
| 0030 | Encrypt OAuth tokens at rest in SQLite database | open | critical | security |
| 0031 | Create Hpoll.Admin.Tests project for admin portal page models | open | high | testing |
| 0032 | Expand TokenRefreshService tests (currently only 3 tests for 140 lines) | open | high | testing |
| 0033 | Fix timing-dependent tests in Worker test suite | open | high | testing, bug |
| 0034 | Add missing edge case tests for EmailRenderer | open | medium | testing |
| 0035 | Add DbContext model configuration tests (unique constraints, cascades) | open | medium | testing |
| 0036 | Add PollingService multi-hub and batch cleanup tests | open | medium | testing |
| 0037 | Improve code coverage for Hpoll.Admin (0% coverage) | open | high | testing |
| 0038 | Add XML documentation comments to public interfaces and services | open | high | documentation |
| 0039 | Add architecture overview and development instructions to README | open | medium | documentation |
| 0040 | Expand .env.example to cover all configuration options | open | medium | documentation |
| 0041 | Add inline comments for complex algorithms and design decisions | open | low | documentation |
| 0042 | Add security response headers (CSP, X-Frame-Options, HSTS) | open | high | security |
| 0043 | Hue API response errors array is never checked after deserialization | open | medium | bug |
| 0044 | Silent catch blocks swallow JSON parsing exceptions without logging | open | high | bug, code-quality |
| 0045 | 401 during polling does not trigger immediate token refresh or status change | open | high | bug |
| 0046 | Disabled sensors are not filtered during polling | open | medium | bug |
| 0047 | Invalid TimeZoneId crashes email sending for all subsequent customers | open | medium | bug |
| 0048 | Persist Data Protection keys and add Secure flag to session cookie | open | medium | security |
| 0049 | HealthEvaluator class is dead code — never registered or used | open | low | code-quality |
| 0050 | Remove or fix orphaned entrypoint.sh | open | low | bug |
| 0051 | BatteryAlertThreshold boundary condition — device at exactly threshold level is neither shown nor alerted | open | medium | bug |
| 0052 | Pin NuGet and Docker dependency versions for reproducible builds | open | low | security, enhancement |
| 0053 | RegisterApplicationAsync leaks full Hue API response body in exception message | open | medium | bug, security |
| 0054 | Introduce EmailMessage model to simplify IEmailSender interface | open | medium | enhancement, code-quality |
| 0055 | Filter DeviceReadings by ReadingType at database level in activity summary queries | open | medium | enhancement, performance |
| 0056 | Duplicate JSON parsing of motion readings doubles memory and CPU cost | open | medium | enhancement, performance |
| 0057 | Add configuration validation tests for boundary conditions | open | medium | testing, bug |
| 0058 | Add CI coverage thresholds and codecov.yml configuration | open | medium | enhancement, testing |
| 0059 | Add missing HueApiClient error path tests | open | medium | testing |
| 0060 | Consolidate Docker CI tag-building script into reusable action or shared script | open | low | enhancement |
| 0061 | Admin portal Docker container binds to all network interfaces | open | low | security |
| 0062 | Sequential email sending limits scalability for large customer counts | open | low | enhancement, performance |
| 0063 | Add HTTPS redirect middleware for admin portal in production | open | medium | security |
| 0064 | Sanitize exception messages before persisting in PollingLog | open | medium | security |
| 0065 | OAuth callback should use signed state parameter instead of session-based CSRF | open | medium | security, enhancement |
| 0066 | Fix CLAUDE.md inaccuracies: CI trigger pattern and missing battery mention | open | low | documentation |
| 0067 | Entity default values use DateTime.UtcNow at instantiation, not persistence time | open | medium | enhancement, code-quality |
| 0068 | IEmailRenderer XML doc comment is misleading — hardcodes configurable values | open | medium | documentation |
| 0069 | PollingService motion detection comment is inaccurate per Hue API docs | open | medium | documentation |
| 0070 | DeviceReading inline comments outdated — missing battery reading type | open | medium | documentation |
| 0071 | EmailSchedulerService.ExecuteAsync error retry path has 0% test coverage | open | medium | testing |
| 0072 | OAuthCallback page is AllowAnonymous but performs sensitive operations | open | medium | security |
| 0073 | Missing CancellationToken propagation in Admin page handlers | open | low | enhancement, code-quality |
