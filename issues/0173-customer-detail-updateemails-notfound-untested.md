---
id: 173
title: "Customer Detail POST handlers missing invalid-customer NotFound tests"
status: closed
closed: 2026-03-15
created: 2026-03-15
author: claude
labels: [testing]
priority: low
---

## Description

Two POST handlers on the Customer Detail page return `NotFound()` when the customer ID is invalid but lack dedicated tests for this path:

1. **`OnPostUpdateEmailsAsync`** (line 111): Returns `NotFound()` for invalid customer ID — no test.
2. **`OnPostToggleStatusAsync`** (line 228): Returns `NotFound()` for invalid customer ID — no test.

Other handlers (`OnPostUpdateNameAsync`, `OnPostUpdateSendTimesAsync`, `OnPostUpdateTimeZoneAsync`, `OnPostRegisterHubAsync`) have explicit invalid-customer-returns-NotFound tests.

**Location:** `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs`, lines 111, 228; `tests/Hpoll.Admin.Tests/Customers/DetailModelTests.cs`

**Category:** coverage-gap, test-quality (pattern inconsistency)

**Severity:** low — simple guard clauses, but inconsistent coverage across identical patterns.

**Recommendation:** Add tests:
- `OnPostUpdateEmailsAsync_InvalidCustomer_ReturnsNotFound`
- `OnPostToggleStatusAsync_InvalidCustomer_ReturnsNotFound`

## Comments

### claude — 2026-03-15

Fixed: Added both recommended tests to `tests/Hpoll.Admin.Tests/Customers/DetailModelTests.cs`:
- `OnPostUpdateEmailsAsync_InvalidCustomer_ReturnsNotFound`
- `OnPostToggleStatusAsync_InvalidCustomer_ReturnsNotFound`

All tests pass. Total test count: 488.
