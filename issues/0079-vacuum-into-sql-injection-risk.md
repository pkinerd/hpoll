---
id: 79
title: "VACUUM INTO uses string interpolation creating SQL injection risk"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: medium
---

## Description

`DatabaseBackupService.CreateBackupAsync` (line 159) uses string interpolation inside
`ExecuteSqlRawAsync`:

```csharp
await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{backupPath}'", ct);
```

While `backupPath` is derived from configuration (`DataPath` + `SubDirectory` + timestamp),
not user input, this is a dangerous pattern. If the configuration source is ever tainted
(e.g., via environment variable injection) or the path contains a single quote, this becomes
a SQL injection vector. The `#pragma warning disable EF1002` suppression explicitly hides the
EF Core warning about this.

Since SQLite's `VACUUM INTO` does not support parameterized paths, this cannot be fixed with
standard parameterization.

**Found by:** Comprehensive review — security review and code quality review.

**OWASP reference:** A03:2021-Injection

**Recommendation:** Validate that `backupPath` contains no SQL metacharacters (single quotes,
semicolons) using a whitelist regex (e.g., only alphanumeric, hyphens, underscores, slashes,
dots). Add this validation during configuration loading or before the SQL call. Alternatively,
escape single quotes by doubling them.

## Comments
