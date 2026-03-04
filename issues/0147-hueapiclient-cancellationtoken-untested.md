---
id: 147
title: "HueApiClient CancellationToken propagation untested"
status: open
created: 2026-03-04
author: claude
labels: [testing]
priority: low
---

## Description

Every async method in `HueApiClient` accepts a `CancellationToken`, but the test file (`HueApiClientTests.cs`, 32 tests) never tests cancellation behavior. All tests use `CancellationToken.None`.

**Note:** `HueApiClient` performs no custom cancellation logic — it merely forwards the token to `HttpClient.SendAsync`. Testing cancellation here is essentially testing the .NET HTTP stack. The test mock's `SendAsync` does not call `cancellationToken.ThrowIfCancellationRequested()`, so adding such tests would also require modifying the mock.

**Recommendation:** This is a "belt and suspenders" improvement. If desired, add one test with a pre-cancelled token and an enhanced mock that checks the token, to verify the token is being forwarded correctly.

**Found by:** Comprehensive review — unit testing review.

## Comments

### critical-review — 2026-03-04

Critical review: ADJUST. Corrected test count from 28 to 32. Noted that HueApiClient performs no custom cancellation logic — it merely forwards the token to HttpClient.SendAsync. Testing this is essentially testing the .NET HTTP stack. Low practical value.
