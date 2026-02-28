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
