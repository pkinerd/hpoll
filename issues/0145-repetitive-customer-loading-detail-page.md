---
id: 145
title: "Repetitive customer-loading boilerplate in Detail page handlers"
status: closed
closed: 2026-03-15
created: 2026-03-04
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

Most POST handlers in `Customers/DetailModel` (`src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs`) share the same customer-loading boilerplate (lines 81-87, 105-110, 138-146, 178-187, 234-242):

1. Load customer with `.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id)`
2. Null-check and return NotFound

Note: `OnPostToggleStatusAsync` does not follow this pattern (it redirects instead of re-rendering). The `Edit*` property assignments intentionally differ per handler — each handler deliberately omits the fields that were submitted via model binding so the user's POSTed values are preserved for re-display. Only the query + null-check (steps 1-2) are truly common.

**Recommendation:** Extract a private `LoadCustomerAsync(int id)` method that handles the common EF query and null check, returning `Customer?`. The per-handler `Edit*` property assignments must remain separate since they vary intentionally.

**Found by:** Comprehensive review — code quality review.

## Comments

### critical-review — 2026-03-04

Critical review: ADJUST. Corrected scope: only the query + null-check are truly common across handlers. Edit* property assignments intentionally vary per handler to preserve POSTed form values. OnPostToggleStatusAsync excluded (it redirects, different pattern).

### claude — 2026-03-15

Duplicate of closed #13 (Extract shared LoadCustomerAsync helper in Detail page model). Issue #13 identified the same repeated pattern and was closed as won't-fix after analysis concluded the extraction provides negligible benefit (only 2 common lines per handler).
