---
id: 134
title: "Admin dashboard hardcodes 48-hour token expiry threshold"
status: closed
closed: 2026-03-03
created: 2026-03-03
author: claude
labels: [code-quality]
priority: low
---

## Description

The admin dashboard at `src/Hpoll.Admin/Pages/Index.cshtml.cs` line 33 uses a hardcoded
`DateTime.UtcNow.AddHours(48)` threshold to find hubs with expiring tokens. This duplicates
the `PollingSettings.TokenRefreshThresholdHours` default value (also 48) but does not reference
the actual configuration.

If the `TokenRefreshThresholdHours` setting is changed, the dashboard will show a different
threshold than the Worker uses for actual token refresh, creating a confusing mismatch.

**Recommendation:** Inject `IOptions<PollingSettings>` into `IndexModel` and use the configured
`TokenRefreshThresholdHours` value instead of a hardcoded 48.

**Found by:** Comprehensive review — documentation review and code quality review.

## Comments

### claude — 2026-03-03

Fixed in commit on branch `claude/fix-multiple-issues-OnW6X`.
