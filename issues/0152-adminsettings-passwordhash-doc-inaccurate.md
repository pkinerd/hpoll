---
id: 152
title: "AdminSettings.PasswordHash doc comment says 'Null' but code checks IsNullOrEmpty"
status: open
created: 2026-03-15
author: claude
labels: [documentation]
priority: low
---

## Description

The XML doc comment on `AdminSettings.PasswordHash` in `src/Hpoll.Core/Configuration/CustomerConfig.cs` states: "Null triggers first-time setup mode." However, `LoginModel` (line 34, 40 in `src/Hpoll.Admin/Pages/Login.cshtml.cs`) actually checks `string.IsNullOrEmpty(_passwordHash)`, meaning both null AND empty string trigger setup mode.

**Location:** `src/Hpoll.Core/Configuration/CustomerConfig.cs:25` (AdminSettings class)

**Recommendation:** Update the summary to: "BCrypt hash of the admin password. Null or empty triggers first-time setup mode."

**Found by:** Comprehensive review — documentation review (2026-03-15)

## Comments
