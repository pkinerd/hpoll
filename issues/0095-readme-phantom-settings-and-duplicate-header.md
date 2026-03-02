---
id: 95
title: "README references nonexistent PollingSettings and has duplicate section header"
status: closed
closed: 2026-03-02
created: 2026-03-02
author: claude
labels: [documentation]
priority: medium
---

## Description

The README.md settings reference table has two issues:

1. **Phantom settings** (lines 37-38): Lists `Polling:HealthFailureThreshold` (default 3) and `Polling:HealthMaxSilenceHours` (default 6), but these properties do not exist in the `PollingSettings` class at `src/Hpoll.Core/Configuration/CustomerConfig.cs`. They are not used anywhere in the codebase. The same phantom settings also appear in:
   - The `appsettings.Production.json` example block (lines 156-157)
   - The `docker-compose.yml` inline environment example (lines 189-190)

   This misleads developers into thinking these settings are functional, and anyone copying the example blocks will include non-functional configuration.

2. **Duplicate section header** (lines 49-52): There are two `**Hue app**` section headers. The first at line 49 is immediately followed by `**Security**`, making it an orphaned/stray entry. The actual Hue app settings appear under the second header at line 52.

**Recommendation:**
- Remove the phantom `PollingSettings` entries from **all three locations**: the settings table (lines 37-38), the appsettings example (lines 156-157), and the docker-compose example (lines 189-190)
- Remove the duplicate `**Hue app**` header at line 49

**Location:** `README.md:37-38, 49-52, 156-157, 189-190`

## Comments

### claude — 2026-03-02

Comprehensive review (documentation) found additional detail:
Documentation review found additional README issues:
- Missing `Backup` section in settings reference table (`Backup:IntervalHours`, `Backup:RetentionCount`, `Backup:SubDirectory`)
- Line 49: duplicate "Hue app" header row (copy-paste artifact before "Security" section)
- `Security:EnableHsts` documented in settings table but missing from Full Docker Compose example
- Production appsettings example on lines 153-154 references the phantom `HealthFailureThreshold`/`HealthMaxSilenceHours` settings

### claude — 2026-03-02

Fixed: Removed phantom Polling:HealthFailureThreshold and Polling:HealthMaxSilenceHours from the settings table, appsettings.Production.json example, and docker-compose.yml example. Removed duplicate 'Hue app' section header.
