---
id: 16
title: "Extract shared DB/HttpClient bootstrap between Worker and Admin Program.cs"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

Database path resolution, DbContext registration, HttpClient registration, and WAL mode pragma are duplicated between:

- `Worker/Program.cs` lines 20-33, 61
- `Admin/Program.cs` lines 28-42, 79

**Recommendation:** Create `ServiceCollectionExtensions` in `Hpoll.Core` or `Hpoll.Data` with methods like `AddHpollDatabase(IConfiguration)` and `AddHueApiClient(IConfiguration)`.

## Comments
