---
id: 100
title: "ExchangeAuthorizationCodeAsync error path has no test coverage"
status: closed
closed: 2026-03-02
created: 2026-03-02
author: claude
labels: [testing]
priority: low
---

## Description

**Note:** Both `ExchangeAuthorizationCodeAsync` and `RefreshTokenAsync` are one-liner delegations to the same private `PostTokenRequestAsync` method. The error handling in `PostTokenRequestAsync` is already fully exercised by `RefreshTokenAsync_On401_ThrowsHttpRequestException`. This issue is about test completeness for the public method entry point, not a real coverage gap — zero additional lines of code would be covered.

The `HueApiClient.ExchangeAuthorizationCodeAsync` method has a test for the success path (`ExchangeAuthorizationCodeAsync_SendsCorrectRequest`) but **no test for the error path** — e.g., what happens when the Hue API returns a non-success status code like 400 or 401.

The `PostTokenRequestAsync` private method throws `HttpRequestException` on failure. While `RefreshTokenAsync_On401_ThrowsHttpRequestException` tests this path for refresh tokens, the identical code path for authorization code exchange is not tested.

**Note:** Issue #0059 ("Add missing HueApiClient error path tests") was closed, but this specific gap appears to remain.

**Location:** `tests/Hpoll.Core.Tests/HueApiClientTests.cs`

**Recommendation:**
Add a test `ExchangeAuthorizationCodeAsync_OnFailure_ThrowsHttpRequestException` that configures a 400 or 401 response and verifies `HttpRequestException` is thrown with the correct status code.

## Comments

### claude — 2026-03-02

Implemented: added ExchangeAuthorizationCodeAsync error path test in HueApiClientTests

