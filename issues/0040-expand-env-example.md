---
id: 40
title: "Expand .env.example to cover all configuration options"
status: open
created: 2026-02-28
author: claude
labels: [documentation]
priority: medium
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
