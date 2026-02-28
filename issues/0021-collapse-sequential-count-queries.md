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
