---
id: 52
title: "Pin NuGet and Docker dependency versions for reproducible builds"
status: open
created: 2026-03-01
author: claude
labels: [security, enhancement]
priority: low
---

## Description

All NuGet packages use floating wildcard versions (e.g., `Version="8.0.*"`, `Version="3.*"`) in `.csproj` files, and Docker base images use major-version tags (`mcr.microsoft.com/dotnet/sdk:8.0`) without SHA digests.

**Impact:** Builds are not reproducible. A compromised or buggy patch release could be silently pulled into a build. Docker images could change between builds.

**Affected files:**
- `src/Hpoll.Core/Hpoll.Core.csproj`
- `src/Hpoll.Data/Hpoll.Data.csproj`
- `src/Hpoll.Email/Hpoll.Email.csproj`
- `src/Hpoll.Worker/Hpoll.Worker.csproj`
- `src/Hpoll.Admin/Hpoll.Admin.csproj`
- `Dockerfile`, `Dockerfile.admin`

**Recommended fix:**
1. Pin exact NuGet package versions and consider using `Directory.Packages.props` for centralized version management
2. Pin Docker base images to specific digests (e.g., `@sha256:...`) or at minimum full version tags
3. Consider using `dotnet restore --locked-mode` with a NuGet lock file

**Source:** Security review findings S5.1, S5.2

## Comments
