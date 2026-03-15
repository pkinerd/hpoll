---
id: 144
title: "OAuthCallbackModel manually maps tokens instead of using ApplyTokenResponse"

closed: 2026-03-15
created: 2026-03-04
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

When creating a new hub in the OAuth callback (`src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs`, lines 122-131), token response properties are manually mapped to the `Hub` entity. However, the `Hub.ApplyTokenResponse` extension method in `HubExtensions.cs` already does exactly this.

The existing hub update path at line 109 correctly uses `ApplyTokenResponse`, but the new hub creation path manually sets `AccessToken`, `RefreshToken`, and `TokenExpiresAt`.

**Note:** `ApplyTokenResponse` also sets `UpdatedAt` (which the manual mapping does not) and conditionally preserves the existing `RefreshToken` if the response is empty (irrelevant for new hub creation but a semantic difference). Using `ApplyTokenResponse` requires moving from object initializer syntax to post-construction mutation.

**Recommendation:** After creating the `Hub` object with non-token fields, call `hub.ApplyTokenResponse(tokenResponse, DateTime.UtcNow)` instead of manually setting token fields. This ensures token-mapping logic is maintained in one place and also sets `UpdatedAt` consistently.

**Found by:** Comprehensive review — code quality review.

## Comments

### critical-review — 2026-03-04

Critical review: ADJUST. Added note that ApplyTokenResponse also sets UpdatedAt (changing behavior slightly) and conditionally preserves existing RefreshToken. Fix requires restructuring from object initializer to post-construction mutation.
