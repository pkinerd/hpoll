---
id: 58
title: "Add CI coverage thresholds and codecov.yml configuration"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, testing]
priority: medium
---

## Description

The CI pipeline collects code coverage but has no thresholds or gates configured. Coverage can regress without any CI signal.

**Current state:**
- `fail_ci_if_error: false` in the Codecov upload step — build passes even if Codecov fails entirely
- No `codecov.yml` configuration file — no coverage targets, PR comment behavior, flags, or component thresholds
- Two test projects produce separate coverage XML files; merge behavior relies on Codecov defaults

**Recommended fix:**

1. Create a `codecov.yml` at the repository root:
```yaml
coverage:
  status:
    project:
      default:
        target: 70%
        threshold: 2%
    patch:
      default:
        target: 80%
```

2. Consider setting `fail_ci_if_error: true` once coverage reporting is stable

3. Add flag definitions to separate Core vs Worker test coverage

4. Add component/path coverage requirements for critical paths (e.g., `src/Hpoll.Core/Services/`)

**File:** `.github/workflows/build-and-test.yml:60`

**Source:** Code coverage review findings CC4, CC5

## Comments
