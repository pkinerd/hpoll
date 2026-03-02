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

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID**

The issue correctly identifies that line 159 of `DatabaseBackupService.cs` uses string
interpolation inside `ExecuteSqlRawAsync`, that the `#pragma warning disable EF1002`
suppression is present on line 158, and that SQLite's `VACUUM INTO` does not support
parameterized paths. The quoted code snippet matches the source exactly. These factual
observations are all accurate.

However, the issue significantly overstates the risk and misclassifies the finding in
several ways:

**1. Calling this "SQL injection" is technically inaccurate.**

SQL injection, per OWASP A03:2021, describes attacks where *untrusted user-supplied input*
is incorporated into SQL statements without adequate validation. In this codebase, the
`backupPath` is assembled from three components (lines 148-150):

```csharp
var timestamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmmss");
var backupFileName = $"hpoll-{timestamp}.db";
var backupPath = Path.GetFullPath(Path.Combine(_backupDirectory, backupFileName));
```

- The **filename** (`hpoll-{timestamp}.db`) uses `DateTime.ToString("yyyyMMdd-HHmmss")`,
  which can only produce digits and a single hyphen. This is inherently safe.
- The **directory** (`_backupDirectory`) is built in the constructor (line 38) from
  `DataPath` (a configuration value, default `"data"`) and `SubDirectory` (a
  `BackupSettings` property, default `"backups"`).
- The path is canonicalized through `Path.GetFullPath`, which normalizes it.

No user input touches this path at any point in the call chain. The `DataPath` value comes
from `appsettings.json` or server environment variables. In Docker deployments (the intended
production mode), it is hardcoded as `ENV DataPath=/app/data` in both `Dockerfile` and
`Dockerfile.admin`. Calling this "SQL injection" conflates a defense-in-depth concern with
an actual vulnerability class.

**2. The threat model requires pre-existing full server compromise.**

The issue hypothesizes "environment variable injection" as an attack vector. However, an
attacker who can set arbitrary environment variables on the host (or inside the container)
already has privileges far exceeding anything SQL injection into a local SQLite file could
grant. They could directly modify or replace the database file, inject malicious binaries,
read secrets from memory, or exfiltrate data. Exploiting `VACUUM INTO` interpolation would
be the least efficient attack path available to such an attacker. This is not a meaningful
escalation scenario.

**3. The OWASP A03:2021 classification is inappropriate.**

OWASP A03:2021 (Injection) targets scenarios where applications fail to validate *user-
controlled input* before passing it to an interpreter. Server-side configuration values set
by the deploying operator are not "user input" in the OWASP threat model. By this logic,
every application that interpolates a `DataPath` configuration into a `Data Source=` connection
string (as this very codebase does at `Program.cs` line 28:
`options.UseSqlite($"Data Source={dbPath}")`) would also be flagged as "injection" -- which
would be absurd.

**4. The `#pragma warning disable EF1002` is documented and appropriate.**

The issue's phrasing that the suppression "explicitly hides the EF Core warning" implies
deception. In reality, line 157 contains a clear comment:
`// VACUUM INTO requires a string literal, not a parameter -- path is from configuration, not user input`.
This is the standard practice when an analyzer warning has been evaluated and determined to
be a false positive for the specific use case. The developer made an informed, documented
decision.

**5. The SQLite limitation is correctly noted** -- `VACUUM INTO` does not support parameterized
paths. This is accurate and is the core reason why string interpolation is used here.

**6. The priority (medium) is too high.**

Given that (a) no user input flows into the path, (b) the filename component is
deterministically safe, (c) exploitation requires pre-existing server compromise, and (d) the
pattern is already documented with an explanatory comment, this is at most a `low`-priority
code hygiene item, not a `medium`-severity security finding.

**7. The recommendation is disproportionate but not entirely without merit.**

A whitelist regex on the backup path is a reasonable defensive coding practice for catching
accidental misconfiguration (e.g., a `DataPath` that happens to contain a single quote). But
framing it as security mitigation against injection overstates the purpose. A simpler approach
-- such as `backupPath.Replace("'", "")` or simply validating that `DataPath` is a valid
filesystem path -- would be equally effective and more proportionate. The existing
`Path.GetFullPath` call already performs path canonicalization.

**Recommendation:** If this issue is to remain open, it should be reclassified:
- Change `priority` from `medium` to `low`
- Change `labels` from `[security]` to `[code-quality]`
- Retitle to something like "Add path validation guard for VACUUM INTO backup path"
- Remove the OWASP reference, as it does not apply to configuration-sourced values

The factual observations about the code pattern are valid, but the security framing,
classification, and priority are all overstated.

### claude — 2026-03-02

Comprehensive review (security) rates this finding as potentially **HIGH severity** (currently tracked as low):

While the path is derived from configuration (not user input), the security review notes that if the `DataPath` configuration were ever set to a value containing a single quote (via environment variable injection or misconfiguration), it would produce a SQL injection vulnerability. The `#pragma warning disable EF1002` suppression confirms awareness of the risk.

Recommendation: sanitize the path by escaping single quotes (`'` → `''`) or validate that the path contains only expected characters before using it in raw SQL. Add input validation on the `DataPath` configuration value at startup.

OWASP reference: A03:2021-Injection
