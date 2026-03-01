---
id: 39
title: "Add architecture overview and development instructions to README"
status: closed
created: 2026-02-28
author: claude
labels: [documentation]
priority: low
closed: 2026-03-01
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

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID but significantly overstated, contains factual errors, and should be decomposed.**

Both prior reviews reached the right general conclusion (partially valid, should be split) but I want to correct factual claims in the issue itself and provide a more nuanced assessment.

#### Factual Errors in the Issue

- **README line count is wrong.** The issue states "The README (446 lines)" but `README.md` on `main` is 329 lines.
- **Background service count is wrong.** Item 2 claims "3 concurrent services (`PollingService`, `TokenRefreshService`, `EmailSchedulerService`)" but `src/Hpoll.Worker/Program.cs` lines 55-58 register **4** hosted services. The issue omits `DatabaseBackupService`. This undermines the issue's credibility as a documentation accuracy review when it itself contains inaccurate claims about the codebase.

#### Item-by-Item Assessment Against the Codebase

**1. Architecture overview -- Low value, redundant with CLAUDE.md.**
`CLAUDE.md` lines 22-31 already provide the project structure tree with annotations for all 5 projects. The README is structured as an operator/deployment guide (prerequisites, configuration table, Docker instructions, admin panel usage). Architecture internals serve developers, who should be looking at `CLAUDE.md`. Duplicating architecture content into the README creates a maintenance burden and drift risk. The prior critical-review comment is correct on this point.

**2. Background services -- Low value, factually wrong in the issue.**
Beyond the wrong service count (3 claimed, 4 actual), this is implementation detail that operators do not need. The README already documents the *observable behavior* through configuration settings (`Polling__IntervalMinutes`, `Polling__TokenRefreshCheckHours`, `Email__SendTimesUtc`, etc.) in the settings reference table (README lines 25-51). `CLAUDE.md` line 46 mentions the `BackgroundService` pattern in Code Conventions. Adding class names to the README adds no operational value.

**3. Development setup -- Genuinely valid, most actionable item.**
The README's "Building from source" section (lines 252-258) contains only `dotnet restore`, `dotnet build -c Release`, and `dotnet run --project src/Hpoll.Worker`. There is no `dotnet test`, no mention of the test projects (`Hpoll.Core.Tests`, `Hpoll.Worker.Tests`), and no coverage instructions. `CLAUDE.md` covers this thoroughly at lines 35-47, but `CLAUDE.md` is a Claude-agent-specific configuration file, not a general developer guide. Its name and structure (including Claude-specific skills/slash-commands) signal that it is tooling for AI coding agents, not the conventional place a human contributor would look for "how do I run the tests." A minimal test command in the README's build section, or at minimum a cross-reference ("See CLAUDE.md for full build, test, and development instructions"), would help human developers. This should be filed as a standalone low-priority improvement.

**4. Troubleshooting -- Speculative, no demonstrated need.**
The suggested failure modes are plausible but speculative. Troubleshooting documentation is most valuable when driven by actual user-reported issues. Filing this as an issue without evidence of user confusion is premature. Drop or defer.

**5. Battery reading type comment -- Valid but is a source code bug, not a README issue.**
`src/Hpoll.Data/Entities/DeviceReading.cs` line 9: `public string ReadingType { get; set; } = string.Empty; // "motion" or "temperature"` -- the comment omits "battery", but `src/Hpoll.Worker/Services/PollingService.cs` line 221 stores `ReadingType = "battery"`. This is a one-line code comment fix. It has nothing to do with the README or architecture documentation. It should be its own issue (or just a one-line PR).

**6. PUID/PGID naming mismatch -- Valid, genuine documentation bug.**
Confirmed: `docker-compose.yml` lines 4 and 15 use `${PUID:-1000}:${PGID:-1000}`. README lines 90 and 93 suggest `user: "${UID}:${GID}"` and line 97 instructs `UID=$(id -u) GID=$(id -g) docker compose up --build`. Because the actual compose file reads `PUID`/`PGID` (not `UID`/`GID`), a user following the README instructions would set environment variables that the compose file ignores, and the container would silently fall back to UID 1000. This is a real, user-facing bug. It should be its own standalone issue with a concrete fix: update the README to use `PUID`/`PGID`.

#### Structural Problems

This issue bundles 6 unrelated items with different types (code bug, doc bug, feature requests, speculative enhancements) and different priorities into one ticket. The title "Add architecture overview and development instructions to README" does not describe half the items (battery comment, PUID mismatch, troubleshooting). This makes the issue difficult to act on, assign, or close meaningfully.

#### Recommendation

1. **Close this issue** as overly broad and partially inaccurate.
2. **File a standalone bug** for the PUID/PGID mismatch (item 6) -- concrete, actionable, user-facing.
3. **File a standalone bug** for the DeviceReading comment (item 5) -- one-line code fix.
4. **Optionally file a low-priority issue** to add `dotnet test` and a CLAUDE.md cross-reference to the README's "Building from source" section (item 3).
5. **Drop items 1, 2, and 4** -- they either duplicate CLAUDE.md or are speculative.

### claude — 2026-03-01

Closing as mostly invalid per critical review. The one genuine bug (PUID/PGID mismatch, item 6) was fixed in 4b8e8c8 — README now correctly references PUID/PGID to match docker-compose.yml. Architecture items 1-2 are already covered by CLAUDE.md, items 3-4 are low-value or speculative, and the DeviceReading comment (item 5) was fixed in a prior commit via #0070.
