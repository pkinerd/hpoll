---
id: 37
title: "Improve code coverage for Hpoll.Admin (0% coverage)"
status: open
created: 2026-02-28
author: claude
labels: [testing]
priority: high
---

## Description

The `Hpoll.Admin` project has **0% code coverage** — it is not included in any test project's coverage collection.

Current coverage by project (merged across both test runs):
- **Hpoll.Core**: 92.3% line coverage (205/222 lines)
- **Hpoll.Data**: 95.5% line coverage (147/154 lines)
- **Hpoll.Email**: 99.6% line coverage (256/257 lines)
- **Hpoll.Worker**: 87.0% line coverage (389/447 lines)
- **Hpoll.Admin**: 0% (not tested)

Notable gaps within tested projects:
- `HueApiClient.EnableLinkButtonAsync`: 58.8% line, 25% branch
- `HueApiClient.RegisterApplicationAsync`: 79.3% line, 64.3% branch
- `TokenRefreshService.ExecuteAsync`: 0% line coverage
- `PollingService.ExecuteAsync`: 62.5% line coverage
- `EmailSchedulerService.ExecuteAsync`: 64.3% line coverage

CI build #116 confirms 112 tests all passing (79 Core + 33 Worker).

## Comments
