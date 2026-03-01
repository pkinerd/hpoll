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
