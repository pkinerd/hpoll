---
id: 160
title: "SendTimeDisplayService has no unit tests"
status: open
created: 2026-03-15
author: claude
labels: [testing]
priority: low
---

## Description

`src/Hpoll.Admin/Services/SendTimeDisplayService.cs` contains logic to read effective send times from the database (with fallback to config defaults). It has two public methods — `GetEffectiveDefaultSendTimesUtcAsync` and `GetDefaultSendTimesDisplayAsync` — with no direct unit tests.

The service performs a database lookup, parses comma-separated time strings, and falls back to configuration values on failure.

Note: The service's logic is already indirectly tested through `CreateModelTests` and `DetailModelTests` (which exercise the DB lookup, fallback, and display formatting scenarios). Dedicated unit tests would primarily improve test organization and clarity rather than fill a true coverage gap.

**Recommendation:** Add tests covering:
1. Entry exists in DB with valid times → returns parsed times
2. Entry exists with invalid/empty value → falls back to config
3. No entry in DB → falls back to config
4. Display formatting produces expected output

Tests should go in `tests/Hpoll.Admin.Tests/Services/SendTimeDisplayServiceTests.cs`.
