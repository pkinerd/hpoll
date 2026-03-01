---
id: 16
title: "Extract shared DB/HttpClient bootstrap between Worker and Admin Program.cs"
status: closed
closed: 2026-03-01
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

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID — recommend closing as wontfix. The previous review's conclusion is correct; here is a deeper line-level assessment.**

#### Precise duplication inventory

Compared `src/Hpoll.Worker/Program.cs` (168 lines) and `src/Hpoll.Admin/Program.cs` (99 lines) on the `main` branch.

**Block 1 — DB path resolution + DbContext registration (identical, 7 lines)**
Worker lines 22-28 vs Admin lines 28-34:
```csharp
var dbPath = Path.Combine(
    builder.Configuration.GetValue<string>("DataPath") ?? "data",
    "hpoll.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<HpollDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));
```
Character-for-character identical in both files.

**Block 2 — HttpClient + HueApiClient registration (identical, 6 lines)**
Worker lines 31-36 vs Admin lines 37-42:
```csharp
var pollingSettings = builder.Configuration.GetSection("Polling").Get<PollingSettings>() ?? new PollingSettings();
builder.Services.AddHttpClient("HueApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(pollingSettings.HttpTimeoutSeconds);
});
builder.Services.AddScoped<IHueApiClient, HueApiClient>();
```
Character-for-character identical in both files.

**Block 3 — WAL pragma (NOT identical)**
Worker lines 63-68 run both `MigrateAsync()` and the WAL pragma in a single scope. Admin lines 78-82 run only the WAL pragma. The Worker owns schema migration; the Admin intentionally does not. This is a meaningful semantic difference, not a clean duplicate.

**Block 4 — Options pattern bindings (trivial partial overlap)**
Worker lines 16-19 bind `Polling`, `Email`, `HueApp`, `Backup`. Admin lines 24-26 bind `HueApp`, `Polling`, `Email`. Two lines overlap, but each app binds different subsets (Worker has `BackupSettings`, Admin omits it). These are one-liners not worth extracting.

**Total truly identical, extractable code: ~13 lines** (blocks 1 and 2). That is 7.7% of Worker's 168 lines and 13.1% of Admin's 99 lines.

#### Would a shared extension method actually help?

**No.** Five reasons:

1. **Rule of Three not met.** Exactly two consumers exist. The canonical refactoring guideline tolerates duplication until a third occurrence. No third host is planned.

2. **WAL block cannot be cleanly shared.** Worker combines `MigrateAsync()` + WAL in one scope (Worker lines 66-67). Admin only runs WAL (Admin line 81). An extension method would need a `bool runMigrations` parameter or would split migration from WAL, changing the Worker's current single-scope pattern. Neither improves clarity.

3. **Dependency graph problems.** The issue suggests placing extensions in `Hpoll.Core` or `Hpoll.Data`. Placing `AddHueApiClient` in `Hpoll.Data` would force it to reference `Hpoll.Core.Services.HueApiClient` and `Hpoll.Core.Configuration.PollingSettings`, coupling the data layer to HTTP/API concerns. Placing both in `Hpoll.Core` would require `Hpoll.Core` to reference `Hpoll.Data` for `HpollDbContext`, creating a circular or inverted dependency. Neither location is clean without restructuring the project graph.

4. **Boot sequence transparency.** Both `Program.cs` files are top-level-statement composition roots — the primary place developers look to understand what each app does. Hiding 13 lines behind `builder.Services.AddHpollDatabase(config)` trades negligible DRY benefit for reduced readability at the most important seam in each application.

5. **The duplication is stable infrastructure plumbing.** DB path resolution and HTTP client setup rarely change. When they do change, it is often in app-specific ways (e.g., Admin might need read-only connection mode, Worker might add connection resilience for concurrent polling).

#### Line reference accuracy of the original issue

The issue cites `Worker/Program.cs` lines 20-33 and 61, and `Admin/Program.cs` lines 28-42 and 79. These are approximately but not precisely correct:
- Worker DB+HTTP block is actually lines 22-36 (not 20-33).
- Worker WAL is at lines 63-68 (not line 61).
- Admin DB+HTTP block is lines 28-42 (matches the issue).
- Admin WAL is at lines 78-82 (not line 79 alone).

#### Conclusion

The duplication is real (13 identical lines across two blocks) but does not warrant extraction. The cost of introducing a shared extension method — dependency graph complexity, reduced boot-sequence transparency, awkward WAL/migration split — outweighs the benefit of deduplicating 13 stable, self-explanatory lines across two fundamentally different application hosts. Agree with the prior review: **close as wontfix**.

### claude — 2026-03-01

Closing: Wontfix: ~19 lines not worth abstracting — two fundamentally different apps, falls under Rule of Three
