---
id: 190
title: "DST spring-forward gap crash in activity summary and email renderer"
status: open
created: 2026-03-30
author: claude
labels: [bug]
priority: high
---

## Description

The activity summary window-bucketing code in both `Detail.cshtml.cs` and
`EmailRenderer.cs` calls `TimeZoneInfo.ConvertTimeToUtc()` without the DST-safe
wrapper that `SendTimeHelper.SafeConvertToUtc` provides.

**Locations:**
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs` line ~335
- `src/Hpoll.Email/EmailRenderer.cs` line ~99

If a window boundary falls in a DST spring-forward gap (e.g., 02:00–03:00 in
US/EU timezones), `ConvertTimeToUtc` throws `ArgumentException: The supplied
DateTime represents an invalid time`. This would crash the customer detail page
load and email rendering for any customer in an affected timezone during the
spring DST transition.

**Recommendation:**
Expose `SendTimeHelper.SafeConvertToUtc` as `public static` (currently `private
static`) and call it in both locations instead of the raw `ConvertTimeToUtc`.

*Found during comprehensive review (code quality review).*

## Comments
