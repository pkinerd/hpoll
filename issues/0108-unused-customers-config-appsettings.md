---
id: 108
title: "Unused Customers configuration key in appsettings.json"
status: closed
created: 2026-03-02
author: claude
labels: [code-quality]
priority: low
closed: 2026-03-03
---

## Description

The Worker's `appsettings.json` includes a `"Customers": []` key at the top level that is not used anywhere in the current codebase.

While `HpollSettings.Customers` exists in `CustomerConfig.cs`, it is never bound or read — customers are managed exclusively through the database and admin portal. This is a leftover from an earlier configuration-driven approach.

**Location:** `src/Hpoll.Worker/appsettings.json`, line 27

**Recommendation:**
1. Remove the `"Customers": []` entry from `src/Hpoll.Worker/appsettings.json`
2. Remove the `HpollSettings`, `CustomerConfig`, and `HubConfig` classes from `src/Hpoll.Core/Configuration/CustomerConfig.cs` — these are **confirmed dead code** with zero references outside their own definition file. The other settings classes in the same file (`PollingSettings`, `EmailSettings`, `HueAppSettings`, `BackupSettings`) are actively used across 24+ files and must be preserved.

**Note:** `HpollSettings` is potentially misleading because it wraps `PollingSettings`, `EmailSettings`, etc. — classes that are bound individually via `Configure<T>()`, not through `HpollSettings` as a root.

## Comments
