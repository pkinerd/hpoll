---
id: 12
title: "Extract Hub.ApplyTokenResponse to eliminate 3-location token update duplication"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: high
---

## Description

The pattern of applying a `HueTokenResponse` to a `Hub` entity is repeated in three locations:

1. `TokenRefreshService.cs` lines 99-105
2. `Hubs/Detail.cshtml.cs` lines 71-75
3. `Hubs/OAuthCallback.cshtml.cs` lines 101-107

All three perform:
```csharp
hub.AccessToken = tokenResponse.AccessToken;
if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
    hub.RefreshToken = tokenResponse.RefreshToken;
hub.TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
hub.UpdatedAt = DateTime.UtcNow;
```

**Recommendation:** Add an extension method `hub.ApplyTokenResponse(HueTokenResponse response)` in `Hpoll.Data.Entities` or `Hpoll.Core`.

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority upgraded medium->high. There are **4 duplication sites, not 3** (PollingService.cs lines 253-259 is missed). **Latent bug**: OAuthCallback.cshtml.cs line 102 unconditionally overwrites RefreshToken without the IsNullOrEmpty guard present in the other 3 sites. Behavioral inconsistencies: Worker uses _timeProvider (testable), Admin uses DateTime.UtcNow (not mockable). Line numbers wrong for 2 of 3 cited locations. Proposed method needs DateTime utcNow parameter.

### claude (critical review) — 2026-03-01

**Verdict: VALID issue with significant inaccuracies in both the original description and the prior review. Priority should be medium, not high.**

#### Duplication site count: 4 update sites, not 3

The original issue lists 3 locations. The prior review correctly identified a 4th in `PollingService.cs`. Here are all four verified against the `main` branch:

| # | File | Actual lines | RefreshToken guard | Time source | Sets UpdatedAt |
|---|------|-------------|-------------------|-------------|----------------|
| 1 | `src/Hpoll.Worker/Services/TokenRefreshService.cs` | 118-124 | Yes (`IsNullOrEmpty`) | `_timeProvider` | Yes |
| 2 | `src/Hpoll.Worker/Services/PollingService.cs` | 254-259 | Yes (`IsNullOrEmpty`) | `_timeProvider` | **No** |
| 3 | `src/Hpoll.Admin/Pages/Hubs/Detail.cshtml.cs` | 117-121 | Yes (`IsNullOrEmpty`) | `DateTime.UtcNow` | Yes |
| 4 | `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs` | 101-107 | **No** (unconditional) | `DateTime.UtcNow` | Yes |

There is also a 5th location at `OAuthCallback.cshtml.cs` lines 117-126 that sets the same three token fields in an object initializer when creating a new Hub. This is a different pattern (entity construction vs. update) and would not benefit from the proposed extension method.

#### Line number accuracy

The original issue's line numbers are all wrong:
- TokenRefreshService.cs: issue says 99-105, actual is **118-124** (off by ~19 lines)
- Detail.cshtml.cs: issue says 71-75, actual is **117-121** (off by ~46 lines)
- OAuthCallback.cshtml.cs: issue says 101-107, actual is **101-107** (correct)

#### What is actually duplicated and what differs

The code is NOT identically duplicated across all sites. Key differences:

1. **Missing `UpdatedAt` in PollingService** (lines 254-259): The PollingService token refresh does NOT set `hub.UpdatedAt` after refreshing tokens. This is a latent bug -- after a 401-triggered token refresh, the hub's `UpdatedAt` timestamp remains stale. (`UpdatedAt` is set later at line 296 only on the successful-poll path, not on the 401-catch-and-refresh path.) This strengthens the case for extraction since the method would enforce consistency.

2. **Missing `IsNullOrEmpty` guard in OAuthCallback** (line 102): The OAuthCallback unconditionally overwrites `RefreshToken`. The prior review flagged this correctly. This may be intentional during initial OAuth (the response always contains a fresh refresh token), but it is inconsistent, and a shared method would normalize the behavior.

3. **Time source divergence**: Worker services use `_timeProvider.GetUtcNow().UtcDateTime` (testable via `TimeProvider`), while Admin pages use `DateTime.UtcNow` directly. An extension method cannot simply use `DateTime.UtcNow` internally -- it needs a `DateTime utcNow` parameter, as the prior review noted. This adds a parameter to every call site, reducing the ergonomic benefit somewhat.

4. **Extra fields in OAuthCallback**: Location 4 also sets `HueApplicationKey`, `Status`, and `ConsecutiveFailures` alongside the token fields. An `ApplyTokenResponse` method would only cover 3-4 of the 7 assignment lines there, limiting the deduplication benefit at that particular site.

#### Prior review accuracy check

The prior review claims "Priority upgraded medium->high," but the original issue was already marked `priority: high` -- there is no upgrade. The prior review's identification of the 4th site (PollingService.cs) and the OAuthCallback guard inconsistency are correct. The claim about "line numbers wrong for 2 of 3 cited locations" is correct. However, the prior review does not identify the missing `UpdatedAt` in `PollingService.cs` (it only mentions the OAuthCallback bug), missing a second latent inconsistency.

#### Is "high" priority justified?

**No. Medium is more appropriate.** Rationale:
- The duplication involves 5-7 lines per site across 4 locations. This is real but modest duplication.
- The inconsistencies (missing `UpdatedAt` in PollingService, missing guard in OAuthCallback) are low-severity bugs -- they do not cause data corruption or security issues.
- The behavioral differences between sites (time source, extra fields) mean the extraction is not a trivial "move identical code" refactor. It requires design decisions about the method signature.
- This is a code quality improvement, not a correctness fix or a change that unblocks other work.

#### Is the proposed extraction practical?

**Yes, but with caveats.** A reasonable signature would be:

```csharp
public static void ApplyTokenResponse(this Hub hub, HueTokenResponse response, DateTime utcNow)
{
    hub.AccessToken = response.AccessToken;
    if (!string.IsNullOrEmpty(response.RefreshToken))
        hub.RefreshToken = response.RefreshToken;
    hub.TokenExpiresAt = utcNow.AddSeconds(response.ExpiresIn);
    hub.UpdatedAt = utcNow;
}
```

This would cleanly cover locations 1-3 and partially cover location 4 (which would still need to set `HueApplicationKey`, `Status`, and `ConsecutiveFailures` separately). The object-initializer site (location 5) cannot use an extension method at all.

**Placement**: The method should live in `Hpoll.Data` (as an extension on the `Hub` entity) since both `Hpoll.Worker` and `Hpoll.Admin` reference `Hpoll.Data`. Placing it in `Hpoll.Core` would require `Hpoll.Core` to reference `Hpoll.Data`, which may create a circular dependency depending on the project graph.

#### Summary

The issue identifies a real duplication problem and proposes a reasonable solution. However, the description contains wrong line numbers for 2 of 3 cited locations, misses a 4th duplication site, and incorrectly claims all sites perform identical code (they do not). The extraction would fix two latent consistency bugs (missing `UpdatedAt` in PollingService and missing `IsNullOrEmpty` guard in OAuthCallback) as a side effect, which is the strongest argument for doing this work. Priority should be **medium**.
