---
id: 127
title: "Customer create form shows wrong send time default and doesn't allow blank for system default"
status: closed
created: 2026-03-02
author: claude
labels: [bug]
priority: medium
closed: 2026-03-03
---

## Description

When creating a new customer in the admin portal, the Email Send Times field has two issues:

1. **Wrong default displayed**: The Create page model hardcodes `SendTimesLocal = "19:30"` as the default value (`src/Hpoll.Admin/Pages/Customers/Create.cshtml.cs:34`). This value is baked into the form field, but it may not match the system-wide default configured in `EmailSettings.SendTimesUtc`. If the system default send times are changed in configuration, the create form will still show "19:30".

2. **Cannot leave blank to use system default**: The form requires a valid HH:mm value and does not allow the field to be left blank. If a user clears the field and submits, validation fails. However, the `SendTimeHelper.ComputeNextSendTimeUtc()` method already supports a fallback hierarchy — when `sendTimesLocal` is empty, it falls back to the configured `defaultSendTimesUtc` from `EmailSettings`, and ultimately to 08:00 UTC as a last resort. This fallback behavior is unreachable from the create form because the hardcoded default always populates the field.

### Expected behavior

- The send time field should either show the actual system default from configuration, or be left blank with a placeholder indicating the default will be used.
- Leaving the field blank should be allowed and should result in the customer using the system-wide default send times (via the existing fallback in `SendTimeHelper`).

### Affected files

- `src/Hpoll.Admin/Pages/Customers/Create.cshtml.cs` — hardcoded `"19:30"` default on line 34, validation rejects empty input
- `src/Hpoll.Admin/Pages/Customers/Create.cshtml` — form field with placeholder `"19:30"`
- `src/Hpoll.Core/Services/SendTimeHelper.cs` — already supports empty `sendTimesLocal` with fallback logic

## Comments
