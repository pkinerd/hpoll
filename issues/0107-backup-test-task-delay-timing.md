---
id: 107
title: "DatabaseBackupService test uses Task.Delay for timestamp separation"
status: open
created: 2026-03-02
author: claude
labels: [testing, code-quality]
priority: low
---

## Description

The test `CreateBackupAsync_MultipleBackups_CreatesSeparateFiles` in `DatabaseBackupServiceTests.cs` (line 271-281) uses `await Task.Delay(1100)` between two backup calls to ensure different timestamps in the filename (which uses seconds-level granularity).

This makes the test slow (adds 1.1 seconds) and introduces a timing dependency. Under heavy CI load, if both backup calls happen within the same second despite the delay, the test could fail.

**Location:** `tests/Hpoll.Worker.Tests/DatabaseBackupServiceTests.cs`, lines 271-281

**Recommendation:**
`DatabaseBackupService` **already has** a `TimeProvider` abstraction injected (constructor parameter at line 29, used at line 148 for filename timestamps). The test simply doesn't take advantage of it.

**Fix:** Pass a `FakeTimeProvider` (from `Microsoft.Extensions.Time.Testing`) into the test's `CreateService` helper, advance its time between the two `CreateBackupAsync` calls, and remove the `Task.Delay` entirely. Zero production code changes needed.

The primary motivation is test speed (eliminating 1.1 seconds of wasted time) and code hygiene (using the abstraction that already exists), not preventing flaky failures — the 100ms buffer over the 1-second boundary makes timing collisions virtually impossible.

## Comments
