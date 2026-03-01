---
id: 80
title: "CLAUDE.md project structure has multiple inaccuracies"
status: open
created: 2026-03-01
author: claude
labels: [documentation]
priority: medium
---

## Description

The project structure section in CLAUDE.md has several inaccuracies:

1. **Missing test project**: Only lists `Hpoll.Core.Tests` and `Hpoll.Worker.Tests`, but
   `Hpoll.Admin.Tests` also exists with substantial test files (159 tests).

2. **Hpoll.Core description inaccurate**: Says "Interfaces, models, configuration, core
   services (HueApiClient)" but omits the `Constants/` directory (`CustomerStatus`, `HubStatus`,
   `DeviceTypes`, `ReadingTypes`), `BuildInfo.cs`, and `SendTimeHelper`.

3. **Hpoll.Data description inaccurate**: Mentions "config seeder" but no config seeder class
   exists. Should be "EF Core DbContext, entities, migrations, hub extensions".

4. **Hpoll.Worker description incomplete**: Says "polling, token refresh, email scheduler" but
   omits `DatabaseBackupService` and `SystemInfoService`.

**Found by:** Comprehensive review — documentation review.

**Recommendation:** Update the Project Structure section to match the actual codebase.

## Comments
