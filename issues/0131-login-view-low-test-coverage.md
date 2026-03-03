---
id: 131
title: "Login.cshtml Razor view has uncovered rendering paths"
status: closed
closed: 2026-03-03
created: 2026-03-03
author: claude
labels: [testing]
priority: low
---

## Description

The Login Razor view at `src/Hpoll.Admin/Pages/Login.cshtml` has uncovered rendering paths in
integration tests. The code-behind (`Login.cshtml.cs`) has near-100% coverage across 15 unit
tests, so all business logic is well-tested. The uncovered areas are purely view-layer rendering
of static markup.

**Uncovered view areas:**
- Environment-based styling (lines 9-10, 15-16): Production/Development switch arms for
  `navColor` and `favicon` (only the default `_ =>` arm is tested)
- Setup mode conditional rendering (lines 59, 66): the password hash generator form
- Login mode error display (lines 93, 95): error message rendering

Note: Razor view branch coverage numbers are inflated by compiler-generated async state machine
branches and null checks, so the raw percentages overstate the actual gap.

**Recommendation:** The highest-value addition would be a single integration test that renders
the Login page in setup mode (null password hash), as this is the one path with meaningfully
different rendered output. The environment-color and error-message rendering tests have low
cost-benefit ratio since they test framework behavior rather than application logic.

**Found by:** Comprehensive review — code coverage analysis.

## Comments

### claude — 2026-03-03

Fixed in commit on branch `claude/fix-multiple-issues-OnW6X`.
