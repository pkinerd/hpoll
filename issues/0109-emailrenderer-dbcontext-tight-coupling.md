---
id: 109
title: "EmailRenderer directly references DbContext rather than abstraction"
status: closed
closed: 2026-03-03
created: 2026-03-02
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

The `EmailRenderer` class in `Hpoll.Email` takes `HpollDbContext` as a direct constructor dependency and performs complex multi-table EF Core queries (with `.Include()`, `.Where()`, `.GroupBy()`, etc.) directly within the rendering logic.

**Location:** `src/Hpoll.Email/EmailRenderer.cs` lines 16, 20-25, 27-138

**Problem:**
- The Email project has a hard dependency on Hpoll.Data, creating coupling between the rendering layer and data access layer
- Rendering logic and data retrieval are interleaved in the same methods
- Changes to the database schema or query patterns require modifying the renderer

**Mitigating factors:**
- The existing test suite (25+ tests in `EmailRendererTests.cs`) successfully tests `EmailRenderer` using an in-memory SQLite `DbContext`, so testability is not a practical concern
- `Hpoll.Email` is only consumed by `Hpoll.Worker`, which already references both `Hpoll.Data` and `Hpoll.Email` — no second consumer is blocked
- The dependency graph is acyclic (`Email` → `Data` → `Core`), so this is a coupling choice, not a layering violation

**Recommendation:**
This is a low-priority structural improvement. If the project grows or the Email project gains additional consumers, consider introducing a data access interface in `Hpoll.Core` (e.g., `IActivityDataProvider`). For now, the more actionable concern is the unbounded battery query (lines 135-138) which fetches all historical battery readings without a time filter and deduplicates in memory — adding a server-side "latest per device" query would be a more impactful improvement.

## Comments

### claude — 2026-03-03

Fixed the actionable part: bounded battery query to 7-day window and added .AsNoTracking() to eliminate unbounded scan. The broader DbContext abstraction is deferred as the issue notes — no second consumer exists and testability is already adequate.
