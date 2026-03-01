---
id: 38
title: "Add XML documentation comments to public interfaces and services"
status: open
created: 2026-02-28
author: claude
labels: [documentation]
priority: high
---

## Description

Out of ~50+ public members across the codebase, only **one** has XML documentation (`IEmailRenderer.RenderDailySummaryAsync`).

**Highest priority undocumented APIs:**

1. **`IHueApiClient`** (9 methods, 0 doc comments): A new developer must read the implementation to understand `EnableLinkButtonAsync`, what exceptions `RefreshTokenAsync` throws, or the difference between `accessToken` and `applicationKey`.

2. **`IEmailSender`** (2 overloads, 0 doc comments): No indication of exception behavior or null vs empty parameter semantics.

3. **`HealthEvaluator`** (2 public methods): The difference between "not healthy" and "needs attention" is unclear without reading the code.

4. **All configuration classes** (`PollingSettings`, `EmailSettings`, `HueAppSettings`): ~30 properties with zero doc comments explaining valid ranges or defaults.

5. **All entity classes**: Status field valid values, `Value` JSON format, cascade delete relationships.

**Zero usage of `<param>`, `<returns>`, `<exception>` tags anywhere in the codebase.**

## Comments

### claude — 2026-03-01

**Comprehensive review findings:** The documentation review identified these specific high-priority gaps:

1. **IEmailRenderer** (`IEmailRenderer.cs:6`): Existing doc comment hardcodes "28 hours" and "4-hour windows" but these are configurable via `SummaryWindowHours` and `SummaryWindowCount`. The actual query window is `totalHours + windowHours = 32` hours. Comments become incorrect if configuration changes.

2. **HueApiModels** (`Models/HueApiModels.cs`): 15 model classes with zero docs. Critical omissions:
   - `HueMotionReport.Changed` means "last time the value of this property is changed" (per Hue API docs), NOT when motion was first detected
   - `HuePowerState.BatteryState` has three values: `normal`, `low`, `critical`
   - `owner.rid` points to the parent `device` resource

3. **PollingService motion comment** (`PollingService.cs:123-126`): States the boolean "resets quickly" which is imprecise. Should reference Hue API docs: the sensor returns to `false` after its timeout period (seconds to minutes), and `changed` reflects when the property value last changed.

4. **Configuration classes**: `BatteryPollIntervalHours` default of 84 (3.5 days), `DataRetentionHours`, `SendTimesUtc` format, and the relationship between `TokenRefreshCheckHours`/`TokenRefreshThresholdHours` all need documentation.

5. **OAuthCallback**: No docs explaining why it's `[AllowAnonymous]` (OAuth callbacks can't carry auth cookies) or the 5-step registration sequence.

### claude — 2026-03-01

**Comprehensive review update:** The documentation review identified 32 specific findings across the codebase. Key highlights that add detail to this issue:

**High-priority undocumented interfaces/classes:**
1. `IHueApiClient` (9 public methods, zero XML docs) — most critical interface in the project
2. `IEmailSender` (2 overloads, no param docs) — unclear whether CC/BCC accepts null vs empty
3. `HueApiModels.cs` (13 model classes, zero docs) — should map to Hue CLIP v2 endpoints
4. `HueApiClient.cs` — Remote API URL constants (`ClipV2BaseUrl`, `RemoteApiBaseUrl`) lack comments explaining cloud-routed vs local bridge distinction
5. `HueTokenResponse` — `ExpiresIn` is in seconds (OAuth2 spec) but undocumented

**Specific model documentation needs (from Hue API cross-reference):**
- `HueMotionData.MotionReport` (nullable) — absence means motion data is invalid/unavailable, replacing the deprecated `motion_valid` boolean
- `HuePowerState.BatteryState` — valid values are specifically `normal`, `low`, `critical` per API docs; only present for battery-powered devices
- `HueApiClient.EnableLinkButtonAsync` uses v1 Remote API endpoint (`/api/0/config`) — needs comment explaining the `0` placeholder username
- `HueApiClient.RegisterApplicationAsync` returns a `username` that becomes the `hue-application-key` for CLIP v2 — not documented

**Configuration classes (all in `CustomerConfig.cs`):**
- `BatteryPollIntervalHours` (default 84) — no comment explaining 3.5 days rationale
- `HealthFailureThreshold`, `HealthMaxSilenceHours` — no units or range docs
- `SummaryWindowHours`, `SummaryWindowCount` — no docs explaining windowed aggregation

**Overall:** Only 1 XML doc comment exists in the entire `src/` directory, and even that one is inaccurate (see below).

### claude — 2026-03-01

**Consolidated from #0068 and #0069 (closed as subsets of this issue).**

Two specific inaccurate comments to fix as part of this documentation effort:

1. **IEmailRenderer XML doc** (from #0068, `IEmailRenderer.cs:5-9`): The only existing XML doc comment hardcodes configurable values — says "28 hours" and "4-hour windows aligned to midnight" but these are configurable via `SummaryWindowHours`/`SummaryWindowCount`. Also lists 6 windows when default config produces 7. Replace with: "Renders the daily summary email. Covers a configurable period (default 28 hours) bucketed into time windows aligned to multiples of SummaryWindowHours in the customer's timezone."

2. **PollingService motion comment** (from #0069, `PollingService.cs:121-125`): Claims the Hue motion boolean "resets quickly" which oversimplifies. Per Hue CLIP v2 docs: `motion_report.motion` reflects current sensor state, `motion_report.changed` is the timestamp of the last state transition, and the sensor holds `true` for a cooldown period (10-30 seconds). Replace with a comment referencing the actual API behavior and explaining why we use the `changed` timestamp instead.
