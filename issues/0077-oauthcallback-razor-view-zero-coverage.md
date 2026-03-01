---
id: 77
title: "OAuthCallback Razor view has 0% test coverage"
status: open
created: 2026-03-01
author: claude
labels: [testing]
priority: high
---

## Description

The OAuthCallback Razor view (`src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml`) has 0% line
coverage (0/12 lines) and 0% branch coverage (0/24 branches). This is a security-relevant page
that handles OAuth callback results with conditional rendering for success/failure states.

The code-behind (`OAuthCallback.cshtml.cs`) is at 100% coverage, but the Razor template itself
is never exercised by any test.

Uncovered conditional branches include:
- Success vs failure state rendering (line 9)
- Device count display (line 13)
- Hub navigation link (line 20)
- Customer navigation link (line 24)
- Error-state customer link (line 33)

**Found by:** Comprehensive review — code coverage analysis.

**Recommendation:** Add integration tests (similar to those in `Hpoll.Admin.Tests/Integration/`)
that perform GET requests to the OAuth callback endpoint with various query parameters to
exercise the success and failure view branches.

## Comments
