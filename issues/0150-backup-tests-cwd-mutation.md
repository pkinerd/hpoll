---
id: 150
title: "DatabaseBackupServiceTests mutates process-wide CWD creating test isolation risk"
status: open
created: 2026-03-15
author: claude
labels: [testing, code-quality]
priority: low
---

## Description

The `DatabaseBackupServiceTests` class in `tests/Hpoll.Worker.Tests/DatabaseBackupServiceTests.cs` calls `Directory.SetCurrentDirectory(_tempDir)` (line 54) in its `CreateService` helper. This mutates process-wide state that affects all threads and tests running in the same process.

**Location:** `tests/Hpoll.Worker.Tests/DatabaseBackupServiceTests.cs:54`

**Problems:**
1. `Directory.SetCurrentDirectory()` is process-global — if xUnit runs tests in parallel within the same process, other tests may see an unexpected working directory
2. The `Dispose` method does not restore the original working directory, leaving a permanent side effect

**Practical risk is low:** No other test class in `Hpoll.Worker.Tests` depends on CWD, so parallel test interference is unlikely in the current codebase. However, this is a latent hazard.

**Recommendation:**
- Simplest fix: make `_dataPath` an absolute path in the test setup (use the full temp directory path instead of a relative name), removing the need for `SetCurrentDirectory` entirely — zero production code changes required
- Alternative: store the original directory in the constructor and restore it in `Dispose`
- Long-term: refactor `DatabaseBackupService` to derive all paths from absolute `DataPath` config

**Found by:** Comprehensive review — unit testing review (2026-03-15)

## Comments
