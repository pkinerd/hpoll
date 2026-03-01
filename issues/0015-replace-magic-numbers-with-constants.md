---
id: 15
title: "Replace magic numbers and hardcoded color codes with named constants"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: low
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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. **FABRICATED claim**: batchSize=1000 at PollingService.cs:278 does not exist anywhere. **FACTUALLY WRONG**: BatteryAlertThreshold defaults to 60 (not 30). The suggestion to couple dashboard 48-hour threshold to PollingSettings conflates UI display with operational threshold. Multiple line numbers wrong. After removing invalid claims, only minor cosmetic items remain.

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID — mostly cosmetic, several claims inaccurate or overstated. Keep at low priority.**

Claim-by-claim verification against `main` branch:

**1. `Index.cshtml.cs` line 35: Token expiry threshold `48` hours**
VERIFIED but line number is wrong. The `AddHours(48)` is at line 32 (`src/Hpoll.Admin/Pages/Index.cshtml.cs:32`). The suggestion to reuse `PollingSettings.TokenRefreshThresholdHours` is questionable. The `TokenRefreshThresholdHours` setting (default 48) controls when the `TokenRefreshService` proactively refreshes tokens — an operational concern. The dashboard `AddHours(48)` is a UI display filter showing "hubs expiring soon." These happen to share the same value today, but coupling them would mean changing the token refresh threshold also changes the dashboard filter, which is a design smell. A separate named constant could be warranted but reusing the polling setting would be wrong.

**2. `Index.cshtml.cs` line 40: `.Take(10)` for dashboard logs**
VERIFIED but line number is wrong. It is at line 49. This is a simple page-size literal for a dashboard query. Extracting it to a constant is textbook over-engineering — the value has no semantic meaning beyond "show 10 recent items," it is used exactly once, and it is immediately obvious from context.

**3. `Hubs/Detail.cshtml.cs` line 143: `.Take(20)` for polling logs**
VERIFIED but line number is wrong. It is at line 189 in `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs`. Same assessment as above — a single-use page-size literal, extracting it adds indirection with no benefit.

**4. `PollingService.cs` line 278: `const int batchSize = 1000`**
FABRICATED. No `batchSize` variable exists anywhere in the codebase. `PollingService.cs` has no batch processing logic at all. The previous reviewer correctly flagged this. Confirmed via `git grep` which returns zero results for any form of "batchSize" across the entire source tree.

**5. `EmailRenderer.cs` lines 185-192: Motion event cap of `5`, color thresholds**
PARTIALLY CORRECT. The motion event cap of 5 exists but at lines 198-209, not 185-192. Specifically: line 198 has the comment `// Motion activity bars — capped at 5 events`, line 203 has `Math.Min(w.TotalMotionEvents, 5)`, line 204 has `cappedEvents * 20` (percentage calculation), and line 209 has `>= 5 ? "5+"`. Lines 185-192 actually contain the email HTML boilerplate `<body>` and header styling. The cap of 5 is also duplicated in the diversity section at line 239. Extracting this to a constant like `MaxDisplayEvents = 5` would be reasonable.

**6. Color codes repeated multiple times**
VERIFIED and this is the most substantive finding in the issue. The color palette `#e74c3c` (red), `#f39c12` (orange), `#27ae60` (green), `#3498db` (blue) appears in three places within `EmailRenderer.cs` (motion bars at lines 205-207, diversity bars at lines 229-231, battery section at lines 273-275, temperature display at lines 254-256). The same colors also appear in `Detail.cshtml` at line 174 and lines 195/197. However, the `EmailRenderer.cs` colors are embedded in raw HTML string construction — they are effectively **inline CSS in an HTML email template**, not application logic. Named C# constants would help slightly within `EmailRenderer.cs` but would not help the Razor template which uses inline Razor syntax. A shared constants class used by both would add coupling between the email module and the admin module for what are really presentation concerns. The practical benefit is modest.

**7. Session keys `"OAuthCsrf"` and `"OAuthCustomerId"` duplicated**
VERIFIED. `"OAuthCsrf"` appears 3 times across `Detail.cshtml.cs` (line 231) and `OAuthCallback.cshtml.cs` (lines 55, 65). `"OAuthCustomerId"` appears 3 times across `Detail.cshtml.cs` (line 232) and `OAuthCallback.cshtml.cs` (lines 56, 66). Extracting these to a shared static class (e.g., `SessionKeys.OAuthCsrf`) is a reasonable improvement — it prevents typo-based bugs and makes session key usage greppable. This is the most defensible suggestion in the issue.

**8. `BatteryAlertThreshold` and `BatteryLevelCritical` both default to 30**
FACTUALLY WRONG, as the previous reviewer noted. `BatteryAlertThreshold` defaults to 60, `BatteryLevelCritical` defaults to 30, and `BatteryLevelWarning` defaults to 50 (see `src/Hpoll.Core/Configuration/CustomerConfig.cs`). These have distinct semantics: `BatteryAlertThreshold` is the level below which the battery section appears in the email at all, `BatteryLevelCritical` determines the red color threshold, and `BatteryLevelWarning` determines the orange color threshold. The defaults are intentionally different and the semantics do not overlap.

**Additional findings not mentioned in the issue:**
- The `48`-hour threshold also appears in `Detail.cshtml:138` and `Hubs/Detail.cshtml:6` as `remaining.TotalHours > 48` for CSS class selection (token-green/yellow/red), and `24` as `remaining.TotalHours > 24`. These Razor template thresholds are the same visual concept as the dashboard `AddHours(48)` filter.
- The 10-minute deletion safety delay at `Hubs/Detail.cshtml.cs:96` and `Hubs/Detail.cshtml:58` is another magic number not mentioned.
- The `500` character truncation for error messages in `PollingService.cs:289` and four places in `HueApiClient.cs` is another repeated magic number.
- The `60` minute incomplete-window threshold at `EmailRenderer.cs:168` is used once.

**Summary:** Of the 8 claims, 1 is fabricated (batchSize), 1 is factually wrong (battery defaults), and the remaining 6 are verified with wrong line numbers. The only changes worth making are: (a) shared session key constants for `OAuthCsrf`/`OAuthCustomerId`, and (b) possibly a `MaxDisplayEvents = 5` constant in `EmailRenderer`. The color codes are presentation/template concerns that do not meaningfully benefit from C# constants. The `.Take(10)` / `.Take(20)` suggestions are over-engineering. Recommend keeping this at low priority and scoping it down to just the session keys and event cap.
