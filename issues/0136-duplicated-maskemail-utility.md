---
id: 136
title: "Duplicated MaskEmail utility across Worker and Email projects"

closed: 2026-03-15
created: 2026-03-04
author: claude
labels: [enhancement, code-quality]
priority: medium
---

## Description

Two separate `MaskEmail` implementations exist with nearly identical logic:

- `EmailSchedulerService.MaskEmail` (`src/Hpoll.Worker/Services/EmailSchedulerService.cs`, line 224) — handles comma-separated emails
- `SesEmailSender.MaskEmail` (`src/Hpoll.Email/SesEmailSender.cs`, line 26) — handles a single email

The core masking algorithm is the same (show first 2 characters of local part, mask rest, preserve domain). Neither has direct unit tests.

**Recommendation:** Extract `MaskEmail` into a shared static utility class in `Hpoll.Core` (e.g., `Hpoll.Core.Utilities.EmailMasker`). Provide both single-email and multi-email overloads. Add unit tests covering: normal email, short local part (1-2 chars), email with no `@`, empty/null input, comma-delimited list.

**Found by:** Comprehensive review — code quality + unit testing reviews.

## Comments

### critical-review — 2026-03-04

Critical review: CONFIRM. Finding is valid. Two separate MaskEmail implementations with nearly identical logic exist in different projects.
