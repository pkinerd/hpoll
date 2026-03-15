---
id: 142
title: "MaskEmail methods have zero test coverage"
status: open
created: 2026-03-04
author: claude
labels: [testing]
priority: low
---

## Description

Both `MaskEmail` implementations lack direct unit tests verifying output correctness:

- `EmailSchedulerService.MaskEmail` (`internal static`) — handles comma-separated emails, line 224. This method IS indirectly executed by tests that call `ProcessDueCustomersAsync`/`SendCustomerEmailAsync` (through logging calls), so code coverage tools would show the lines as covered. However, no test verifies the actual masked output.
- `SesEmailSender.MaskEmail` (`private static`) — handles single emails, line 26. Indirectly executed via 6 tests in `SesEmailSenderTests.cs` (called before every `SendEmailAsync`), but no test asserts on the masked output.

These are logging-only helper methods — incorrect masking would not affect functionality, only log readability.

**Recommendation:** Add direct unit tests for `EmailSchedulerService.MaskEmail` (accessible via `InternalsVisibleTo`) to verify edge case behavior: normal email, short local part, no `@` symbol, empty/null input, comma-delimited list.

**Found by:** Comprehensive review — unit testing + code coverage reviews.

## Comments

### critical-review — 2026-03-04

Critical review: ADJUST. Lowered priority from medium to low. EmailSchedulerService.MaskEmail is indirectly executed by tests through logging calls (code coverage tools would show lines as covered). Only SesEmailSender.MaskEmail has true zero coverage. These are logging-only helpers; incorrect masking affects only log readability.
