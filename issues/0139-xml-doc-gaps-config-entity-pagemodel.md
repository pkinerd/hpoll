---
id: 139
title: "XML doc comment gaps on configuration, entity, and page model classes"
status: open
created: 2026-03-04
author: claude
labels: [documentation]
priority: low
---

## Description

Multiple classes across the codebase lack XML doc comments. Narrowed to the highest-value targets:

**Configuration classes (highest priority — non-obvious defaults):**
- `PollingSettings` — 7 properties with non-obvious defaults (e.g., `BatteryPollIntervalHours = 84`, `DataRetentionHours = 168`) and no docs
- `EmailSettings` — partial docs (3 properties documented, 6 not)
- `HueAppSettings` — no class or property docs

**Key undocumented logic:**
- `HubExtensions.ApplyTokenResponse` — important refresh token preservation logic undocumented
- `Hub.ConsecutiveFailures` and `Hub.DeactivatedAt` — non-obvious entity properties

Admin page models, service implementations (whose interfaces are well-documented), and simple entity classes are lower priority and excluded from this scope.

**Found by:** Comprehensive review — documentation review.

## Comments

### critical-review — 2026-03-04

Critical review: ADJUST. Lowered priority from medium to low. Narrowed scope to configuration classes with non-obvious defaults and key undocumented logic (ApplyTokenResponse, Hub properties). Excluded admin page models and service implementations whose interfaces are already documented.
