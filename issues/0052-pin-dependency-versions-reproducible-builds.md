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

### Critical Review — 2026-03-01

**Verdict: PARTIALLY VALID — low priority, mislabeled as security**

#### Claim Accuracy

**Claim: "All NuGet packages use floating wildcard versions"** -- **FALSE.** While most packages do use wildcards, several are already pinned to exact versions:
- `Microsoft.Extensions.Hosting` is pinned to `8.0.1` (in `Hpoll.Worker.csproj`)
- `xunit` is pinned to `2.4.2` (in all three test `.csproj` files)
- `xunit.runner.visualstudio` is pinned to `2.4.5` (in all three test `.csproj` files)

The following packages do use floating wildcards: `Microsoft.Extensions.Http` (`8.0.*`), `Microsoft.Extensions.Logging.Abstractions` (`8.0.*`), `Microsoft.Extensions.Options` (`8.0.*`), `Microsoft.EntityFrameworkCore.*` (`8.0.*`), `AWSSDK.SimpleEmail` (`3.*`), `Moq` (`4.*`), `Microsoft.NET.Test.Sdk` (`17.*`), `coverlet.collector` (`6.*`), `JunitXml.TestLogger` (`3.*`).

**Claim: "Docker base images use major-version tags without SHA digests"** -- **TRUE.** Both `Dockerfile` and `Dockerfile.admin` use `mcr.microsoft.com/dotnet/sdk:8.0`, `mcr.microsoft.com/dotnet/runtime:8.0`, and `mcr.microsoft.com/dotnet/aspnet:8.0` without SHA digests.

**Claim (implied): CI has no version pinning discipline** -- **FALSE.** The GitHub Actions workflow (`.github/workflows/build-and-test.yml`) already pins all Actions by full SHA digest (e.g., `actions/checkout@692973e3d937129bcbf40652eb9f2f61becf3332`). This demonstrates the project already follows pinning best practices where it matters most (CI supply chain).

#### Existing Infrastructure

- `Directory.Build.props` exists but is used only for build metadata injection (branch, commit, timestamp), not for centralized version management.
- No `Directory.Packages.props` exists (no Central Package Management).
- No `packages.lock.json` files are in use (`RestorePackagesWithLockFile` is not set).

#### Risk Assessment

The "security" label is **overstated** for this project's context:

1. **NuGet wildcards on Microsoft first-party packages (`8.0.*`)**: These resolve within the `8.0.x` patch range, which follows strict semantic versioning. Microsoft .NET patch releases are security/bugfix only. The supply-chain risk of auto-accepting patch updates from Microsoft's signed NuGet feed is negligible, and arguably *beneficial* since it means security patches are adopted automatically.

2. **AWSSDK.SimpleEmail (`3.*`)**: This is a wider float (any `3.x.y`), but AWS SDK for .NET also follows semver and the `3.*` range stays within the v3 major version. Risk is low, though pinning to `3.7.*` or similar would be a modest improvement.

3. **Docker base images (`sdk:8.0`, `runtime:8.0`, `aspnet:8.0`)**: This is the most legitimate concern. Microsoft updates these images with new patch versions, and while rare, a broken image could affect builds. However, for a small monitoring service built in CI (not a regulated/audited deployment pipeline), the practical impact is minimal.

4. **Project scale**: hpoll is a single-developer Hue monitoring service. It is not a library consumed by others, not a security-critical financial application, and not subject to regulatory reproducibility requirements. The overhead of maintaining pinned versions, SHA digests, and lock files across all dependencies is disproportionate to the actual risk.

#### Recommendation

- **Relabel** from `security` to `enhancement` only.
- **Priority should remain `low`** or be downgraded to `wontfix` depending on maintainer preference.
- **If acted on**, the highest-value, lowest-effort change would be enabling NuGet lock files (`RestorePackagesWithLockFile` in `Directory.Build.props` + committing `packages.lock.json`) and using `dotnet restore --locked-mode` in CI. This provides reproducibility without the maintenance burden of manually pinning every version.
- **Docker SHA pinning** adds significant maintenance overhead (digests change with every image update) for minimal benefit at this project's scale. Not recommended unless the project adopts automated dependency update tooling (e.g., Dependabot or Renovate).
- **Central Package Management (`Directory.Packages.props`)** is a nice-to-have for deduplication but is not a security measure and is unnecessary for a solution with only 8 projects.
