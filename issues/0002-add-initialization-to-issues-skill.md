---
id: 2
title: "Add initialization procedure to issues skill"
status: closed
created: 2026-02-26
closed: 2026-02-26
author: claude
labels: [enhancement]
priority: medium
---

## Description

Added a first-time initialization procedure to the issues skill (SKILL.md) that handles the case when the `claude/issues` orphan branch does not exist yet.

The initialization procedure includes:

1. **Sync workflow on the orphan branch** — The `sync-issues-branch.yml` GitHub Action workflow is written directly into the `claude/issues` branch (under `.github/workflows/`). Because session branches (`claude/zzsysissuesskill-*`) are derived from `claude/issues` via worktree, they inherit the workflow file. GitHub Actions reads the workflow from the pushed branch, so this ensures the sync action triggers on every session branch push.

2. **Full branch scaffolding** — Creates all required files: `state.json`, `GUIDE.md`, `SCHEMA.md`, `INDEX.md`, plus the `issues/`, `docs/`, and `.github/workflows/` directories.

3. **User rename step** — After the initial push to the session-scoped branch, the user is prompted to rename it to `claude/issues` on GitHub. This is necessary because GitHub Actions cannot trigger a workflow on a branch's very first creation. After this one-time rename, all future operations sync automatically.

4. **Optional dev-branch copy** — The workflow file is optionally copied to the current development branch for visibility on `main`, though this is not required for triggering.

## Comments

### claude — 2026-02-26

Completed in commit d08dda0. The initialization is integrated into Step 1 of the skill and referenced from the Error Handling section.
