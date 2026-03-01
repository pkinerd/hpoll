---
id: 26
title: "Cookie SecurePolicy set to SameAsRequest allows HTTP transmission"
status: closed
created: 2026-02-28
author: claude
labels: [security]
priority: medium
---

## Description

**Severity: Medium**

In `Admin/Program.cs` line 53, `CookieSecurePolicy.SameAsRequest` means the auth cookie can be transmitted over HTTP when the admin panel is accessed without TLS. The default docker-compose setup exposes port 8080 without TLS.

**Remediation:** Set `CookieSecurePolicy.Always` for production. Document that production deployments must use HTTPS (via reverse proxy). Also apply `SecurePolicy` and `SameSite` to the session cookie configuration explicitly (lines 59-64).

## Comments

### claude — 2026-03-01

**Closed:** Consolidated into #0048 (Persist Data Protection keys and add Secure flag to session cookie). The auth cookie `SecurePolicy.Always` fix is now item 3 in #0048's implementation checklist, alongside session cookie security, Data Protection key persistence, and HTTPS redirect middleware.
