---
id: 105
title: "Timezone display name not HTML-encoded in email renderer"
status: closed
created: 2026-03-02
closed: 2026-03-03
author: claude
labels: [security]
priority: low
---

## Description

The `BuildHtml` method in `EmailRenderer.cs` HTML-encodes `DeviceName` (line 279) but does not encode the `tzName` value interpolated at line 192. The `tzName` comes from `TimeZoneInfo.DaylightName`/`StandardName`, which are OS-provided strings.

Other interpolated values (`WindowSummary.Label`, date/time strings, temperature values) are entirely code-generated from `DateTime` formatting and numeric conversions — they cannot contain HTML-significant characters and do not need encoding.

While no known timezone name contains HTML metacharacters (`<`, `>`, `&`), encoding `tzName` is a cheap defense-in-depth measure.

**Location:** `src/Hpoll.Email/EmailRenderer.cs`, line 192 (the `{tzName}` interpolation)

**Recommendation:**
Apply `Encode()` to the `tzName` value on line 192. No other values in `BuildHtml` require encoding as they are all code-generated.

## Comments
