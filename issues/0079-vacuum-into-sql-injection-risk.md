---
id: 79
title: "VACUUM INTO uses string interpolation creating SQL injection risk"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: low
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

### claude — 2026-03-01

**Critical review: Several flaws in this issue's reasoning. Priority downgraded from medium to low.**

#### Flaw 1: The threat model is inverted — exploitation requires a precondition that makes the attack moot

The issue acknowledges the path is "derived from configuration, not user input" but then speculates about "environment variable injection" as a realistic attack vector. This inverts the threat model: if an attacker can set environment variables on the host, they already have OS-level access and can directly read/modify the SQLite database file, steal AWS credentials, change connection strings, or exfiltrate data. Injecting SQL through a backup path would be the *least* impactful thing they could do. The precondition (compromised host environment) is catastrophic on its own — the supposed consequence (SQL injection) adds negligible incremental risk.

#### Flaw 2: The filename component makes injection structurally impossible through half the path

The issue glosses over the actual path construction. Looking at lines 148–150:

```csharp
var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
var backupFileName = $"hpoll-{timestamp}.db";
var backupPath = Path.GetFullPath(Path.Combine(_backupDirectory, backupFileName));
```

The `backupFileName` is always `hpoll-YYYYMMDD-HHMMSS.db` — only digits, hyphens, a dot, and ASCII letters. This component *cannot* contain SQL metacharacters regardless of any external influence. The only potentially taintable components are `DataPath` (default `"data"`, Docker-hardcoded to `/app/data`) and `BackupSettings.SubDirectory` (default `"backups"`). The issue should have analyzed which specific path segments could theoretically carry injection payload rather than treating the entire `backupPath` as uniformly risky.

#### Flaw 3: The `#pragma warning disable EF1002` characterization is misleading

The issue says the suppression "explicitly hides the EF Core warning about this," implying negligence. In reality, line 157 has a clear comment: *"VACUUM INTO requires a string literal, not a parameter — path is from configuration, not user input."* This is a deliberate, documented engineering decision that acknowledges the SQLite limitation and explains why it's acceptable. Framing it as "hiding" a warning mischaracterizes an informed design choice as a deficiency.

#### Flaw 4: The recommendation about semicolons is technically incorrect

The issue recommends validating for "SQL metacharacters (single quotes, semicolons)." A semicolon inside a single-quoted SQLite string literal is just a literal character — it does *not* terminate the statement or enable statement stacking. The only way to inject is to first break out of the string literal with an unmatched single quote. Listing semicolons as a SQL metacharacter to validate alongside single quotes conflates two unrelated concerns and indicates imprecise understanding of the injection mechanics.

#### Flaw 5: Priority should be low, not medium

Given that: (a) the path comes entirely from trusted configuration with hardcoded defaults, (b) the filename segment is structurally injection-proof, (c) exploitation requires OS-level access that makes the injection moot, and (d) the code already documents the design decision — this is a defense-in-depth code-hygiene observation, not a real vulnerability. Medium priority significantly overstates the actual risk. Downgraded to low.

#### Flaw 6: The recommendation misses the simplest fix

Rather than a custom whitelist regex, the simplest defense-in-depth fix for this specific case is a one-liner: `backupPath.Replace("'", "''")`. This is idiomatic SQLite single-quote escaping and directly addresses the only character that could break out of the string literal. The issue mentions this as an afterthought ("Alternatively, escape single quotes by doubling them") when it should be the primary recommendation given its simplicity and directness.

#### What the issue gets right

The general observation that `ExecuteSqlRawAsync` with string interpolation is a code smell worth noting is valid. The OWASP A03:2021-Injection classification is technically correct. And the fact that `VACUUM INTO` doesn't support parameterized paths is an accurate constraint. These are reasonable observations — they're just significantly overstated in severity and imprecisely analyzed in the writeup.
