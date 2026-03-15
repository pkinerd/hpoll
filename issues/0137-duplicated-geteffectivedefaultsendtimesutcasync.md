---
id: 137
title: "Duplicated GetEffectiveDefaultSendTimesUtcAsync in Create and Detail pages"

closed: 2026-03-15
created: 2026-03-04
author: claude
labels: [enhancement, code-quality]
priority: medium
---

## Description

Both admin page models contain identical copies of two methods:

- `GetEffectiveDefaultSendTimesUtcAsync()` — queries SystemInfo for `email.send_times_utc`, falls back to `_emailSettings.SendTimesUtc`
- `GetDefaultSendTimesDisplayAsync()` — formats the effective send times for display

**Locations:**
- `src/Hpoll.Admin/Pages/Customers/Create.cshtml.cs`, lines 103-122
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs`, lines 284-303

**Recommendation:** Extract into a shared service (e.g., `SendTimeDisplayService`) or a base PageModel class for customer-related pages.

**Found by:** Comprehensive review — code quality review.

## Comments

### critical-review — 2026-03-04

Critical review: CONFIRM. Finding is valid. Identical method pairs exist in both Create and Detail page models.
