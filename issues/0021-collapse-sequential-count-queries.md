---
id: 21
title: "Collapse sequential COUNT queries on Dashboard and About pages"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, performance]
priority: low
---

## Description

Dashboard (`Index.cshtml.cs` lines 28-33) runs 5 sequential COUNT queries (2 customer status, 3 hub status). These could be collapsed into 2 GroupBy queries.

About page (`About.cshtml.cs` lines 48-50) runs 3 sequential COUNT queries. These could be parallelized with `Task.WhenAll` (requires separate DbContext instances, or could be a single raw SQL query).

Low priority since these are admin pages with minimal traffic, but it's a clean improvement.

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Recommend wontfix. Line numbers wrong for both files (Dashboard: 25-30 not 28-33; About: 31-33 not 48-50). Task.WhenAll suggestion is flawed (DbContext not thread-safe). About page counts span 3 different tables (GroupBy impossible). SQLite is in-process with no network round-trip -- savings would be microseconds on tiny tables.
