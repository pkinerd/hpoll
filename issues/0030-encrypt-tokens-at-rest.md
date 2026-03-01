---
id: 30
title: "Encrypt OAuth tokens at rest in SQLite database"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: critical
---

## Description

**Severity: Critical**

OAuth access and refresh tokens in `Hub.cs` are stored in plaintext in SQLite. If the database file is exfiltrated (backup exposure, container escape), all tokens are immediately compromised.

**Remediation:** Encrypt tokens at rest using ASP.NET Core's Data Protection API (`IDataProtector`) before persisting, and decrypt when reading.

## Comments

### claude — 2026-02-28

**Priority upgraded from low to critical** following comprehensive security review.

This is the single most impactful security finding. If the SQLite database file is compromised (via backup leak, volume mount exposure, host compromise, or container escape), ALL Hue OAuth tokens for EVERY customer are immediately usable by an attacker to control their smart home devices. The tokens grant full access to lighting, sensors, and automation on the customer's Hue Bridge.

Additional remediation options beyond Data Protection API:
- SQLCipher for full database encryption
- EF Core value converters for per-column encryption with envelope encryption and a key from a vault/KMS
- At minimum, restrict database file permissions and document the risk

### claude — 2026-03-01

**Comprehensive review note:** The `ConfigSeeder` class (`src/Hpoll.Data/ConfigSeeder.cs:51-72`) supports seeding access tokens, refresh tokens, and application keys from configuration (`HubConfig` properties in `appsettings.json`). This means tokens can end up in configuration files that might be committed to version control or stored in plaintext on disk.

While the current appsettings files have empty values, the design encourages placing tokens in configuration. Consider:
- Logging a warning when tokens are seeded from configuration (to flag accidental usage)
- Documenting that environment variables (not config files) should be used if token seeding is necessary
- Removing the token seeding capability entirely in favor of the OAuth flow
