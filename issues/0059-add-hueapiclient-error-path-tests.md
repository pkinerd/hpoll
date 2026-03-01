---
id: 59
title: "Add missing HueApiClient error path tests"
status: closed
created: 2026-03-01
author: claude
labels: [testing]
priority: medium
closed: 2026-03-01
---

## Description

Several `HueApiClient` methods have untested error/failure paths:

1. **`EnableLinkButtonAsync` failure** — Only the success case is tested. No test for HTTP error status code.
   - Suggested: `EnableLinkButtonAsync_OnFailure_ThrowsHttpRequestException`

2. **`RegisterApplicationAsync` HTTP failure** — No test for HTTP error status code (only "unexpected format" is tested).
   - Suggested: `RegisterApplicationAsync_On401_ThrowsHttpRequestException`

3. **`RegisterApplicationAsync` non-array response** — Line 130 checks `ValueKind == JsonValueKind.Array`. Object response is untested.
   - Suggested: `RegisterApplicationAsync_OnNonArrayResponse_Throws`

4. **`RegisterApplicationAsync` null username** — Line 137 has `?? throw`. Not tested.
   - Suggested: `RegisterApplicationAsync_WithNullUsername_Throws`

5. **`GetResourceAsync` null deserialization** — Line 181 throws `InvalidOperationException`. Not tested.
   - Suggested: `GetMotionSensorsAsync_OnNullDeserialization_Throws`

6. **`PostTokenRequestAsync` null deserialization** — Line 211 throws if token response is null. Not tested.

**File:** `tests/Hpoll.Core.Tests/HueApiClientTests.cs`
**Source file:** `src/Hpoll.Core/Services/HueApiClient.cs`

**Source:** Unit testing review finding UT1.4

## Comments

### claude — 2026-03-01

Resolved: Added all 6 requested error path tests to HueApiClientTests.cs: EnableLinkButtonAsync_OnFailure_ThrowsHttpRequestException, RegisterApplicationAsync_On401_ThrowsHttpRequestException, RegisterApplicationAsync_OnNonArrayResponse_ThrowsInvalidOperationException, RegisterApplicationAsync_WithNullUsername_ThrowsInvalidOperationException, GetMotionSensorsAsync_OnNullDeserialization_ThrowsInvalidOperationException, RefreshTokenAsync_OnNullDeserialization_ThrowsInvalidOperationException. All 6 tests passing.
