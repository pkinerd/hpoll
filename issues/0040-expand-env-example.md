---
id: 40
title: "Expand .env.example to cover all configuration options"
status: open
created: 2026-02-28
author: claude
labels: [documentation]
priority: low
---

## Description

The `.env.example` covers only ~5 of ~25+ available configuration options, omitting:

- All `Polling__*` settings (BatteryPollIntervalHours, DataRetentionHours, HttpTimeoutSeconds, TokenRefresh*, Health*)
- All `Email__*` advanced settings (BatteryAlertThreshold, BatteryLevelCritical/Warning, SummaryWindowHours/Count, ErrorRetryDelayMinutes)
- PUID/PGID for docker-compose
- AWS credential configuration guidance (env vars vs IAM roles)
- Logging configuration
- ASPNETCORE_ENVIRONMENT

Also, no validation ranges are documented anywhere. For example: what happens if `IntervalMinutes` is 0? If `DataRetentionHours` < `SummaryWindowHours * SummaryWindowCount`? If `BatteryLevelCritical` > `BatteryLevelWarning`?

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. **FABRICATED**: Health* settings do not exist anywhere in the codebase. The .env.example has **10 entries** (not ~5) covering all required credentials. AWS credential guidance claim is false (already present). All missing settings have sensible defaults. Validation ranges belong in a separate code issue, not .env.example.
