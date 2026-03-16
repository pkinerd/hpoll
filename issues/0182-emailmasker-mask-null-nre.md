---
id: 182
title: "EmailMasker.Mask(null) lacks null guard"
status: open
created: 2026-03-16
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

The `EmailMasker.Mask(string email)` method does not guard against null input. Calling `Mask(null)` would cause a `NullReferenceException` on `email.IndexOf('@')`.

**Location:** `src/Hpoll.Core/Utilities/EmailMasker.cs`

**Impact:** Low — the primary public entry point `MaskList` already guards against null/empty input at line 29 (`if (string.IsNullOrEmpty(email)) return email`), and the `SesEmailSender` call site passes elements from a list populated by `ParseEmailList` (which won't produce null elements). No current code path passes null directly to `Mask`. This is a defensive hardening improvement.

**Found by:** Unit testing review — missing edge case

**Recommendation:** Add a null guard at the top of `Mask()` — either return `null`/empty string, or throw `ArgumentNullException`. Add a corresponding unit test for `Mask(null)`.

## Comments
