---
id: 32
title: "Expand TokenRefreshService tests (currently only 3 tests for 140 lines)"
status: open
created: 2026-02-28
author: claude
labels: [testing]
priority: high
---

## Description

`TokenRefreshServiceTests` has only 3 tests for a 140-line service. Missing scenarios:

- Token not near expiry (should be skipped)
- Multiple hubs with mixed expiry (only refresh near-expiry ones)
- Refresh returning empty RefreshToken (conditional update logic)
- Verify exactly 3 retry attempts before marking `needs_reauth`
- Inactive hub filtered out by status query
- `UpdatedAt` timestamp verification
- CancellationToken during retry delays

## Comments
