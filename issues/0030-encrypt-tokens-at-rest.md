---
id: 30
title: "Encrypt OAuth tokens at rest in SQLite database"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: low
---

## Description

**Severity: Low**

OAuth access and refresh tokens in `Hub.cs` are stored in plaintext in SQLite. If the database file is exfiltrated (backup exposure, container escape), all tokens are immediately compromised.

**Remediation:** Encrypt tokens at rest using ASP.NET Core's Data Protection API (`IDataProtector`) before persisting, and decrypt when reading.

## Comments
