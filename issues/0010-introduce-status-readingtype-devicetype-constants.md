---
id: 10
title: "Introduce string constants or enums for Status, ReadingType, and DeviceType fields"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: medium
---

## Description

Raw string literals are used throughout the codebase for `Customer.Status`, `Hub.Status`, `DeviceReading.ReadingType`, and `Device.DeviceType` fields with no type safety.

**Status values** (`"active"`, `"inactive"`, `"needs_reauth"`) appear across ~14 files including:
- Entity defaults, Admin page handlers, Worker services, Email renderer

**ReadingType values** (`"motion"`, `"temperature"`, `"battery"`) appear across 4 files. The inline comment on `DeviceReading.ReadingType` says `"motion" or "temperature"` but the code also uses `"battery"` — making the comment stale.

**DeviceType values** (`"motion_sensor"`, `"temperature_sensor"`, `"battery"`) appear in PollingService, EmailRenderer, and Detail page.

**Recommendation:** Create `CustomerStatus`, `HubStatus`, `ReadingType`, and `DeviceType` as either C# enums (with EF Core value conversion) or static string constant classes in `Hpoll.Core`. Replace all raw string literals.

## Comments

### claude — 2026-03-01

**Consolidated from #0070 (closed as subset of this issue).**

When introducing the `ReadingType` enum/constants, also update `DeviceReading.cs` line 10's `Value` comment to include the battery JSON schema: `{"battery_level": 85, "battery_state": "normal"}` (currently only documents motion and temperature formats). The stale `ReadingType` comment on line 9 will be naturally replaced by the enum's own documentation.

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded high->medium. No active bug exists; risk is hypothetical. Issue does not acknowledge EF Core migration cost, Razor view impact, or that static string constants (simpler option) are not evaluated. Test files (~10+) would also need updates.

### claude (critical review) — 2026-03-01

**Verdict: VALID issue, but several claims need correction.**

#### 1. File count verification

The issue claims status values appear across "~14 files." Actual count of unique source files (excluding test files and migrations) containing status/ReadingType/DeviceType raw string literals: **13 files**, not 14. This is close enough to be fair, though the issue conflates different literal categories to reach the number.

Breakdown by category in `src/` (non-test, non-migration):

- **Status literals** (`"active"`, `"inactive"`, `"needs_reauth"`): **13 files** — `Customer.cs`, `Hub.cs`, `Create.cshtml.cs`, `Customers/Detail.cshtml`, `Customers/Detail.cshtml.cs`, `Hubs/Detail.cshtml`, `Hubs/Detail.cshtml.cs`, `OAuthCallback.cshtml.cs`, `Index.cshtml.cs`, `EmailRenderer.cs`, `EmailSchedulerService.cs`, `PollingService.cs`, `TokenRefreshService.cs`
- **ReadingType literals** (`"motion"`, `"temperature"`, `"battery"`): **4 files** — `Customers/Detail.cshtml.cs`, `DeviceReading.cs`, `EmailRenderer.cs`, `PollingService.cs`. This count is accurate.
- **DeviceType literals** (`"motion_sensor"`, `"temperature_sensor"`, `"battery"`): **3 files where literals are used programmatically** — `Customers/Detail.cshtml.cs` (line 279), `EmailRenderer.cs` (line 72), `PollingService.cs` (lines 162, 189, 215). The `Hubs/Detail.cshtml` (line 112) only *displays* `@device.DeviceType` — it does not compare against string literals and would not be affected by this change.

Additionally, **10 test files** would require updates, which the second comment acknowledges but the original description omits entirely.

#### 2. Stale comment — confirmed accurate

The comment on `DeviceReading.cs` line 9 says `// "motion" or "temperature"` but `"battery"` is also used as a ReadingType (in `PollingService.cs` line 221 and `EmailRenderer.cs` line 136). This is indeed stale. The issue correctly identifies this.

#### 3. DeviceType claim — partially inaccurate

The issue states DeviceType values appear "in PollingService, EmailRenderer, and Detail page." This needs clarification:

- `PollingService.cs` — correct: uses `"motion_sensor"` (line 162), `"temperature_sensor"` (line 189), `"battery"` (line 215) as DeviceType values passed to `GetOrCreateDeviceAsync`.
- `EmailRenderer.cs` — correct: filters `d.DeviceType == "motion_sensor"` (line 72).
- `Customers/Detail.cshtml.cs` — correct: filters `d.DeviceType == "motion_sensor"` (line 279).
- `Hubs/Detail.cshtml` — **incorrect inclusion**: merely renders `@device.DeviceType` (line 112) without comparing to any string literal. This is a display-only reference and would not benefit from constants.

#### 4. Enum vs. static string constants — practical assessment

The recommendation proposes "either C# enums (with EF Core value conversion) or static string constant classes" without evaluating the trade-offs:

- **Enums with EF Core value conversion** work well with SQLite and EF Core 8.0 (`HasConversion<string>()`). However, this approach has a meaningful cost: all LINQ queries using `.Where(x => x.Status == SomeEnum.Active)` must go through the converter, and existing migration snapshots store these as `string` columns. Changing to enums would require ensuring the `HasConversion` is applied in the `DbContext` configuration, but would **not** require a new migration because the database column type remains `TEXT` in SQLite. This is feasible.

- **Static string constant classes** (e.g., `public static class HubStatus { public const string Active = "active"; }`) are simpler to adopt, require zero EF Core configuration changes, and can be done incrementally file by file. This approach should be explicitly recommended as the pragmatic first step.

The issue fails to mention that the Razor `.cshtml` views (`Hubs/Detail.cshtml` lines 34, 38, 49, 56, 64 and `Customers/Detail.cshtml` line 107) compare against status string literals directly in markup. Replacing these with constants requires importing the constants namespace in `_ViewImports.cshtml` or using `@using` directives. This is a non-trivial but often-overlooked part of the migration.

#### 5. CustomerStatus vs. HubStatus distinction

The issue correctly proposes separate `CustomerStatus` and `HubStatus` types, since `Customer.Status` accepts only `"active"` / `"inactive"` while `Hub.Status` accepts `"active"` / `"inactive"` / `"needs_reauth"`. These are distinct domains and should not share a single type.

#### 6. Priority assessment

Medium priority is appropriate. There is no active bug — the risk is typo-based errors in future development. The codebase has been consistent with its string values so far. The benefit is purely preventive: compile-time safety, IDE autocompletion, and elimination of the stale comment problem. The scope of change (13 source files + 10 test files = 23 files) is substantial but mechanical.
