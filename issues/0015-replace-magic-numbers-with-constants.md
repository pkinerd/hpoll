---
id: 15
title: "Replace magic numbers and hardcoded color codes with named constants"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: medium
---

## Description

Several hardcoded values are scattered across the codebase without named constants:

- `Index.cshtml.cs` line 35: Token expiry threshold `48` hours (should use `PollingSettings.TokenRefreshThresholdHours`)
- `Index.cshtml.cs` line 40: `.Take(10)` for dashboard logs
- `Hubs/Detail.cshtml.cs` line 143: `.Take(20)` for polling logs
- `PollingService.cs` line 278: `const int batchSize = 1000`
- `EmailRenderer.cs` lines 185-192: Motion event cap of `5`, color thresholds
- `EmailRenderer.cs`: Color codes `"#e74c3c"` (red), `"#f39c12"` (orange), `"#27ae60"` (green), `"#3498db"` (blue) repeated multiple times
- `Detail.cshtml.cs`/`OAuthCallback.cshtml.cs`: Session keys `"OAuthCsrf"` and `"OAuthCustomerId"` duplicated without shared constants
- `EmailSettings`: `BatteryAlertThreshold` and `BatteryLevelCritical` both default to 30 with overlapping semantics

## Comments
