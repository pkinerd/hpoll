---
id: 171
title: "OAuthCallback multi-step registration loses tokens on partial failure"
status: closed
closed: 2026-03-15
created: 2026-03-15
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

The OAuth callback in `OAuthCallbackModel.OnGetAsync` performs a multi-step registration sequence:
1. Exchange auth code for tokens (line 98)
2. Enable link button (line 102)
3. Register application to get `hue_application_key` (line 106)
4. Get bridge ID (line 110)
5. Save hub to database (lines 122 or 140)
6. Test connectivity / get device count (line 146, already wrapped in try/catch as non-fatal)

If steps 1-2 succeed but step 3 or 4 fails, the obtained tokens are never persisted. The user must restart the entire OAuth flow from scratch, including re-authorizing on the Hue developer portal. Additionally, if step 3 succeeds but step 4 fails, an orphaned application key remains on the Hue bridge that can never be reclaimed (see #94).

**Location:** `src/Hpoll.Admin/Pages/Hubs/OAuthCallback.cshtml.cs`, lines 93-160

**Category:** architecture, quality

**Severity:** low — the failure window is narrow (transient errors between steps 1-4 on the same `api.meethue.com` host), the re-authorization cost is moderate (one click on the Hue portal), and a proper fix requires non-trivial UI changes (retry mechanism).

**Recommendation:** Consider persisting partial progress after step 1 to allow retry from step 2 without re-authorization. Note that storing tokens in the session has security implications, and a partial hub record complicates the data model. A simpler alternative would be to catch step 3/4 failures and persist the tokens as a hub in `NeedsReauth`-like state that can be manually retried.

## Comments

### claude — 2026-03-15

Closed as won't fix. The failure window is narrow (transient errors between steps on the same API host), re-authorization cost is low (one click), and a proper fix requires non-trivial UI changes disproportionate to the risk.
