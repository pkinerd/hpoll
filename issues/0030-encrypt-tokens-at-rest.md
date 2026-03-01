---
id: 30
title: "Encrypt OAuth tokens at rest in SQLite database"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: medium
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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded critical->medium. Every scenario granting SQLite access also grants encryption key access (same data volume) unless external KMS is used. **FABRICATED**: ConfigSeeder at src/Hpoll.Data/ConfigSeeder.cs does not exist (removed in 4020a11). Token refresh requires ClientId/ClientSecret from env vars, not DB. Existing mitigations not acknowledged: non-root Docker, authenticated admin, time-limited tokens.

### claude — 2026-03-01 (detailed critical review)

**Verdict: PARTIALLY_VALID -- real but overstated. Priority should remain medium, not critical.**

#### 1. What the issue gets right

The core observation is accurate: `AccessToken`, `RefreshToken`, and `HueApplicationKey` are stored as plaintext `TEXT` columns in `src/Hpoll.Data/Entities/Hub.cs` (lines 9-11). There is zero encryption or Data Protection usage anywhere in the codebase -- confirmed by searching for `DataProtect`, `IDataProtect`, `Encrypt`, `Decrypt`, `Cipher`, and `ProtectAsync`. If someone obtains the SQLite file, they can read every token with `sqlite3 hpoll.db "SELECT AccessToken, RefreshToken FROM Hubs"`.

Additionally, `DatabaseBackupService` (`src/Hpoll.Worker/Services/DatabaseBackupService.cs`) creates periodic `VACUUM INTO` copies under `data/backups/`, meaning plaintext tokens are replicated into backup files on the same volume. This increases the number of files containing sensitive data.

The issue also correctly notes that `HubConfig` in `src/Hpoll.Core/Configuration/CustomerConfig.cs` has `AccessToken` and `RefreshToken` properties, which could be populated from `appsettings.json` or environment variables during seeding. However, the second comment's reference to `ConfigSeeder` at `src/Hpoll.Data/ConfigSeeder.cs` is fabricated -- no such file exists in the codebase.

#### 2. Threat model reality check

The description claims this is "the single most impactful security finding" with severity "Critical." This overstates the actual risk for several reasons:

**Same-volume key colocation problem.** The SQLite database lives at `/app/data/hpoll.db` inside a Docker container. ASP.NET Core Data Protection, the recommended remediation, stores its key ring on the local filesystem by default (typically under `~/.aspnet/DataProtection-Keys/` or a configured path). In this deployment, the key ring would need to be persisted on the same `/app/data` volume to survive container restarts. This means any attacker who exfiltrates the database also has access to the decryption keys. Encryption at rest with colocated keys provides no meaningful protection against database exfiltration -- it only protects against someone who somehow gets the DB file but not the rest of the volume, which is an unusual and narrow scenario.

**Attack scenarios require host-level compromise.** The scenarios listed (backup leak, container escape, volume mount exposure) all require the attacker to already have access to the host filesystem or Docker daemon. At that point, the attacker also has access to:
- Environment variables containing `HueApp__ClientId` and `HueApp__ClientSecret` (needed to actually use stolen refresh tokens)
- The Data Protection key ring (if encryption were implemented)
- The running process memory

**Tokens are time-limited.** Access tokens have a finite lifetime (`TokenExpiresAt` is tracked, `TokenRefreshService` refreshes them proactively). Stolen access tokens become useless after expiry. Stolen refresh tokens require the `ClientId` and `ClientSecret` (from env vars, not the DB) to exchange for new access tokens, so exfiltrating the database alone does not give an attacker the ability to refresh tokens.

**Existing mitigations not acknowledged.** Both Dockerfiles create a non-root `appuser` and run as `USER appuser`. The admin portal uses cookie-based authentication (`CookieAuthenticationDefaults.AuthenticationScheme`). These are not sufficient on their own, but the issue presents no mitigations as existing.

#### 3. Data Protection API is the wrong default recommendation

The blanket recommendation of `IDataProtector` has significant operational problems that the issue does not address:

**Key management complexity.** Data Protection keys must be persisted and shared between the Worker and Admin containers (both access the same DB). The default ephemeral key store means keys are lost on container restart, making all encrypted tokens permanently unreadable. You would need to configure `PersistKeysToFileSystem` pointing to the shared data volume -- which circles back to the colocation problem above.

**Key rotation breaks old data.** Data Protection supports key rotation, but if old keys are pruned or the key ring is corrupted/lost, every encrypted token becomes permanently unrecoverable. This would force re-authentication of every hub -- a significant operational disruption. There is no fallback.

**Debugging and operations.** Encrypted tokens make it impossible to inspect token state during debugging or manual troubleshooting (e.g., checking if a token looks valid, comparing tokens across hubs). The admin portal currently allows viewing token values via `Detail.cshtml.cs` (lines 41-43). Encryption would require decryption in every code path that touches tokens, adding complexity with minimal security benefit given the threat model.

**Migration complexity.** Encrypting existing plaintext tokens requires a data migration that reads every hub's tokens, encrypts them, and writes them back. If this migration fails partway through, you end up with a mix of encrypted and plaintext tokens with no way to tell them apart. An EF Core value converter approach would need a way to detect whether a value is already encrypted.

#### 4. What would actually help

If the goal is defense-in-depth (reasonable for a system controlling physical devices), these approaches are more effective than application-layer encryption with colocated keys:

- **Restrict file permissions on the data volume.** Ensure the SQLite file and backups are owned by `appuser` with mode `0600`. This is cheap and reduces the attack surface without adding code complexity.
- **External KMS (if warranted).** If the threat model truly demands encrypted-at-rest tokens that resist host compromise, use AWS KMS or Azure Key Vault for envelope encryption. The key never touches disk. This is the only approach that addresses the exfiltration scenario meaningfully, but it adds cloud service dependencies and cost.
- **SQLCipher for full-database encryption.** This encrypts the entire DB file transparently, including backups created via `VACUUM INTO`. The passphrase would still need to come from somewhere (env var or KMS), but it provides broader protection than per-column encryption and is simpler to implement.
- **Reduce token lifetime and scope.** Work with the Hue API to minimize token validity windows and request minimal scopes. This limits blast radius regardless of storage encryption.

#### 5. Summary

The issue identifies a real characteristic of the codebase (plaintext token storage) but inflates its severity by not analyzing the actual deployment model, threat vectors, or operational costs of the proposed fix. Data Protection with filesystem-backed keys on the same volume as the database is security theater -- it adds complexity and operational risk without meaningfully improving security against the stated threats. The priority should remain **medium** as a known limitation to address if the deployment model changes (e.g., database backups sent to external storage, multi-tenant hosting) or if an external KMS becomes available. The `HueApplicationKey` field, also stored in plaintext and also a sensitive credential, is not even mentioned in the original issue.
