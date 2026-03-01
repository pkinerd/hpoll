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
