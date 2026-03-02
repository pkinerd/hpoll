---
id: 98
title: "SystemInfoService.ClearAllAsync test verifies exception instead of actual behavior"
status: open
created: 2026-03-02
author: claude
labels: [testing, code-quality]
priority: low
---

## Description

The test `ClearAllAsync_RemovesAllEntries` in `SystemInfoServiceTests.cs` (line 167-179) does **not** verify that `ClearAllAsync` removes all entries. Instead, it asserts that the method throws `InvalidOperationException` because the EF Core InMemory provider does not support raw SQL (`DELETE FROM SystemInfo`).

**Note:** The test author was aware of this limitation — a comment in the test says "We verify the method doesn't crash silently by checking that it throws the expected NotSupportedException." However, the test name is still misleading — it does not verify the intended behavior. The actual clearing logic is **never tested** anywhere in the test suite.

**Location:** `tests/Hpoll.Worker.Tests/SystemInfoServiceTests.cs`, lines 167-179

**Recommendation:**
Replace the test with a SQLite in-memory backed version (like `PollingServiceTests` already uses — `Hpoll.Worker.Tests.csproj` already references `Microsoft.EntityFrameworkCore.Sqlite`) that verifies rows are actually deleted after calling `ClearAllAsync`. No new dependencies required.

## Comments
