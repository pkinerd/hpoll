---
id: 12
title: "Extract Hub.ApplyTokenResponse to eliminate 3-location token update duplication"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: medium
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
