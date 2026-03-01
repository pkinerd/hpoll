---
id: 45
title: "401 during polling does not trigger immediate token refresh or status change"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [bug]
priority: high
---

## Description

**Severity: High**

In `PollingService.cs` lines 224-226, when a 401 Unauthorized is received during hub polling, the code logs a warning and increments `ConsecutiveFailures` but does NOT:
1. Trigger an immediate token refresh attempt
2. Set `hub.Status = "needs_reauth"` to signal urgency
3. Notify the `TokenRefreshService` to act sooner

The `TokenRefreshService` runs on a configurable interval (default 24 hours via `TokenRefreshCheckHours`), meaning a hub could remain broken for up to 24 hours before a refresh is attempted. During this time, all polling cycles for that hub will fail with 401.

**Impact:** Customer monitoring has a potential 24-hour gap after token expiration before automatic recovery. The hub continues to be polled (wasting API calls and generating error logs) without any corrective action.

**Remediation:** On 401, either:
1. Immediately attempt `hueClient.RefreshTokenAsync()` within the polling cycle
2. Set `hub.Status = "needs_reauth"` to stop futile polling attempts
3. Reduce `TokenRefreshCheckHours` or add a mechanism for the polling service to signal the token refresh service

## Comments
