---
id: 138
title: "Admin page models use DateTime.UtcNow instead of TimeProvider"
status: open
created: 2026-03-04
author: claude
labels: [enhancement, code-quality, testing]
priority: low
---

## Description

The Worker project consistently uses `TimeProvider` for time abstraction, making all background services fully testable with deterministic time. However, every Admin page model uses `DateTime.UtcNow` directly, making time-dependent logic untestable:

**Affected files:**
- `Customers/Detail.cshtml.cs` — lines 98, 129, 168-169, 213-214, 228, 308
- `Customers/Create.cshtml.cs` — line 95
- `Hubs/Detail.cshtml.cs` — lines 62, 69, 85, 98, 119 (including 10-minute delete cooling period)
- `Hubs/OAuthCallback.cshtml.cs` — lines 109, 129
- `Index.cshtml.cs` — line 40
- `Login.cshtml.cs` — lines 52, 64, 65 (rate limiter lockout timing)

Additionally, `TimeProvider` is not registered in Admin's `Program.cs` (the Worker registers `TimeProvider.System` as a singleton).

**Narrowed scope:** Focus on the 2-3 handlers with time-sensitive logic worth testing deterministically (hub delete cooling period in `Hubs/Detail.cshtml.cs`, login rate limiter lockout in `Login.cshtml.cs`). The remaining `DateTime.UtcNow` usages (setting `CreatedAt`/`UpdatedAt` timestamps) are simple record-keeping that does not benefit from `TimeProvider` abstraction.

**Recommendation:**
1. Add `builder.Services.AddSingleton(TimeProvider.System)` to Admin's `Program.cs`
2. Inject `TimeProvider` into the 2-3 page models with time-sensitive logic (hub delete cooling period, login lockout)
3. Do not blanket-replace all `DateTime.UtcNow` — only where deterministic testing adds value

**Found by:** Comprehensive review — code quality + unit testing reviews.

## Comments

### critical-review — 2026-03-04

Critical review: ADJUST. Lowered priority from medium to low. Narrowed scope to only the 2-3 handlers with time-sensitive logic (hub delete cooling period, login lockout). Simple timestamp-setting usages do not benefit from TimeProvider abstraction.
