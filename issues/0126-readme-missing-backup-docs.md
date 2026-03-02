---
id: 126
title: "README missing Backup configuration documentation"
status: open
created: 2026-03-02
author: claude
labels: [documentation]
priority: low
---

## Description

The `Backup` configuration section (`Backup:IntervalHours`, `Backup:RetentionCount`, `Backup:SubDirectory`) is fully implemented in `BackupSettings` (`src/Hpoll.Core/Configuration/CustomerConfig.cs`, lines 59-64) and used by `DatabaseBackupService`, but is completely absent from the README settings reference table, `.env.example`, and the `docker-compose.yml` inline example. The Worker's `appsettings.json` includes the `Backup` section, but users have no documentation for these options.

Note: The backup feature works correctly with its sensible defaults (24h interval, 7 backups retained, "backups" subdirectory), so this is a documentation completeness issue rather than a functional gap. Related to #0095 which also tracks README inaccuracies.

**Category:** missing-docs
**Severity:** low
**Found by:** Documentation review (comprehensive review 2026-03-02)

### Recommendation

Add a `**Backup**` section to the README settings reference table documenting:
- `Backup:IntervalHours` — Hours between automatic backups (default: 24)
- `Backup:RetentionCount` — Maximum number of backup files to retain (default: 7)
- `Backup:SubDirectory` — Subdirectory within DataPath for backup files (default: "backups")

Also add `Backup__*` entries to `.env.example` and the full `docker-compose.yml` and `appsettings.Production.json` examples in the README for consistency with other settings sections.

## Comments
