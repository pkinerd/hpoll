# Issues

| ID | Title | Status | Priority | Labels |
|----|-------|--------|----------|--------|
| 0001 | Define objectives and document plan | closed | medium | planning |
| 0002 | Add initialization procedure to issues skill | closed | medium | enhancement |
| 0003 | Setup build pipeline | open | medium | planning |
| 0004 | Setup hosting | open | medium | planning |
| 0005 | Setup CI pipeline for dev/ci | open | medium | planning |
| 0006 | Setup CD pipeline to test and prod | open | medium | planning |
| 0007 | Research API docs for Hue APIs and make available to Claude | closed | medium | documentation, planning |
| 0008 | Implementation plan | open | medium | planning, documentation |
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
| 0024 | Tokens embedded in HTML page source on Hub detail page | open | medium | security |
| 0025 | Logout endpoint lacks antiforgery validation (CSRF logout) | open | medium | security |
| 0026 | Cookie SecurePolicy set to SameAsRequest allows HTTP transmission | open | medium | security |
| 0027 | Rate limiting dictionary has no size bounds (DoS risk) | open | low | security |
| 0028 | Forwarded headers trust all proxies — IP spoofing possible | open | low | security |
| 0029 | CC/BCC email fields lack server-side format validation | open | low | security |
| 0030 | Encrypt OAuth tokens at rest in SQLite database | open | low | security |
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
