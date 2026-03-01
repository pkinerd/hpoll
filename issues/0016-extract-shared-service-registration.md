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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Recommend wontfix. Duplication is real but trivially small (~19 lines, 7.5% overlap). The two Program.cs files are fundamentally different apps. WAL block cannot be cleanly shared (Worker runs MigrateAsync, Admin does not). With exactly 2 consumers, this falls under the Rule of Three. Extraction would obscure boot sequences.
