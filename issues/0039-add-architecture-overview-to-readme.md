---
id: 39
title: "Add architecture overview and development instructions to README"
status: open
created: 2026-02-28
author: claude
labels: [documentation]
priority: low
---

## Description

The README (446 lines) is thorough for deployment but lacks:

1. **Architecture overview**: No explanation of the 5-project structure (`Core`, `Data`, `Email`, `Worker`, `Admin`), their relationships, or overall data flow (poll -> store -> aggregate -> email).

2. **Background services**: No mention that the worker runs 3 concurrent services (`PollingService`, `TokenRefreshService`, `EmailSchedulerService`) with independent schedules.

3. **Development setup**: No instructions for running tests, no mention of test projects, no local development guidance.

4. **Troubleshooting**: No documentation of common failure modes (expired tokens, SES sandbox, incorrect bridge ID, timezone not found).

5. **Battery reading type**: The `DeviceReading.Value` comment only documents "motion" and "temperature", but code also stores "battery". Data retention cleanup behavior is undocumented.

6. **PUID/PGID naming mismatch**: docker-compose.yml uses `PUID`/`PGID` but README references `UID`/`GID`.

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. CLAUDE.md already contains architecture overview (lines 17-56). README has a Building from source section. The PUID/PGID naming mismatch (docker-compose uses PUID/PGID, README says UID/GID) is a genuine bug that should be its own issue. The issue bundles 4+ separate concerns with different priorities.

### critical-review — 2026-03-01

**Verdict: CLOSE as mostly invalid. Split off the one genuine bug into its own issue.**

#### Detailed Analysis

**1. Architecture overview (INVALID -- already covered by CLAUDE.md)**

CLAUDE.md already documents the project structure with all 5 projects (`Core`, `Data`, `Email`, `Worker`, `Admin`), their roles, the tech stack, and data flow context. The README is aimed at operators and end-users who need to deploy, configure, and run hpoll. Architecture internals belong in developer-facing documentation, which is exactly what CLAUDE.md serves. Duplicating the architecture overview into the README would create a documentation sync problem: any refactor that adds, removes, or renames a project would require updating two files that describe the same structure. CLAUDE.md is the single source of truth for contributors and AI coding agents. Adding the same information to the README provides no benefit to the README's actual audience.

**2. Background services detail (INVALID -- wrong audience)**

Knowing that the worker runs `PollingService`, `TokenRefreshService`, and `EmailSchedulerService` is an implementation detail. The README already explains the observable behavior: the service polls hubs, refreshes tokens, and sends emails on a schedule. Operators configure `Polling:IntervalMinutes`, `Polling:TokenRefreshCheckHours`, and `Email:SendTimesUtc` without needing to know the class names. This information is better left in CLAUDE.md or inline code documentation for contributors.

**3. Development setup / test instructions (WEAK -- marginal value)**

The README has a "Building from source" section with `dotnet restore`, `dotnet build`, and `dotnet run` commands. It is true there is no `dotnet test` command in the README. However, CLAUDE.md already provides complete build and test commands including coverage and CI-matching flags. The question is whether a contributor would look at the README for test instructions or at CLAUDE.md. Since CLAUDE.md is the established developer guide (and is what AI agents and contributors are directed to), adding a redundant test section to the README adds maintenance burden for marginal benefit. At most, a one-line note saying "See CLAUDE.md for development and testing instructions" could be added to the README's "Building from source" section.

**4. Troubleshooting section (INVALID -- speculative scope creep)**

The issue requests documentation of "common failure modes (expired tokens, SES sandbox, incorrect bridge ID, timezone not found)." This is a wishlist item, not a gap analysis. Troubleshooting guides should be written based on actual user-reported issues, not speculatively. Writing troubleshooting docs before users encounter these problems creates documentation that may be inaccurate, incomplete, or never consulted. If troubleshooting docs are needed, they should be driven by support tickets or user feedback, not pre-emptively imagined.

**5. Battery reading type / data retention (INVALID -- code-level concern, not README)**

The claim that `DeviceReading.Value` only documents "motion" and "temperature" is a code comment issue, not a README issue. Data retention cleanup behavior is an implementation detail governed by `Polling:DataRetentionHours`, which IS documented in the README's settings reference table. The README already tells operators how to configure retention. Documenting the internal cleanup mechanism belongs in code comments or CLAUDE.md.

**6. PUID/PGID naming mismatch (VALID -- genuine bug)**

Confirmed: `docker-compose.yml` uses `${PUID:-1000}:${PGID:-1000}` but the README instructs users to run `UID=$(id -u) GID=$(id -g) docker compose up --build`. This is a real bug that will cause the user override to silently fail (the container will use UID/GID 1000 regardless of what `UID`/`GID` are set to, since docker-compose reads `PUID`/`PGID`). This should be split into a standalone bug-fix issue since it is a concrete, actionable fix unrelated to the rest of this documentation wish-list.

#### Summary of Concerns

- **Documentation sync problem**: 5 of the 6 items propose duplicating information that already exists in CLAUDE.md. Maintaining the same architecture and development content in two files is a known anti-pattern that leads to drift. The README (329 lines) is already comprehensive for its target audience (operators/deployers). CLAUDE.md (97 lines) is comprehensive for its target audience (developers/agents).
- **Bundled unrelated items**: This issue mixes a real bug (#6) with documentation wishlists (#1-#5). These should never be in the same issue.
- **Wrong audience assumption**: The issue assumes the README should serve developers. The README is structured as an operator's guide (prerequisites, configuration reference, Docker deployment, admin panel usage). CLAUDE.md is the developer guide.

#### Recommendation

1. **Close this issue.**
2. **Create a new bug issue** for the PUID/PGID vs UID/GID mismatch in the README (item #6). This is a concrete fix: change `UID` to `PUID` and `GID` to `PGID` in the README's docker-compose user override instructions.
3. Optionally, add a single line to the README's "Building from source" section: "For development setup, testing, and code conventions, see `CLAUDE.md`." This bridges the two documents without duplicating content.
