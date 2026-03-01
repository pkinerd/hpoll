---
id: 78
title: "XML doc gaps remain on Worker services and configuration classes"
status: open
created: 2026-03-01
author: claude
labels: [documentation]
priority: medium
---

## Description

Follow-up to #38 (closed). The documentation review found extensive XML doc comment gaps remain:

**Worker services (all 5 have zero XML docs):**
- `PollingService` — most complex service, 357 lines, no docs on class or any method
- `TokenRefreshService` — exponential backoff retry logic undocumented
- `EmailSchedulerService` — sleep loop strategy and send time advancement undocumented
- `DatabaseBackupService` — VACUUM INTO backup strategy undocumented
- `SystemInfoService` — ClearAllAsync uses raw SQL, unexplained

**Configuration classes (all 6 have zero XML docs):**
- `HpollSettings`, `CustomerConfig`, `HubConfig`, `PollingSettings`, `EmailSettings`,
  `HueAppSettings`, `BackupSettings` — ~30 properties with no doc comments explaining
  valid ranges, defaults, or semantics. E.g., `BatteryPollIntervalHours = 84` (why 84?),
  `BatteryAlertThreshold` vs `BatteryLevelWarning` (what's the difference?)

**Other gaps:**
- `SesEmailSender` — no docs on AWS SES interaction or FromAddress requirement
- `IEmailSender` interface — no XML docs at all (only undocumented interface)
- `ISystemInfoService` interface — no XML docs
- All 6 entity classes — no XML docs
- `HpollDbContext` — no docs on index rationale in OnModelCreating

**Found by:** Comprehensive review — documentation review.

**Recommendation:** Prioritize configuration classes (developers need to know valid values)
and Worker services (complex business logic). Add `<summary>`, `<param>`, `<returns>`, and
`<exception>` tags.

## Comments
