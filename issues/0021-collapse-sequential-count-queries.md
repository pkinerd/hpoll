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

### claude (critical review) — 2026-03-01

**Verdict: CLOSE AS WONTFIX -- premature optimization with inaccurate details.**

#### Factual Accuracy of the Issue

The issue contains several factual errors:

1. **Dashboard line numbers are wrong.** The issue cites `Index.cshtml.cs` lines 28-33 for "5 sequential COUNT queries." The actual COUNT queries are at lines 25-30 in `src/Hpoll.Admin/Pages/Index.cshtml.cs`:
   - Line 25: `_db.Customers.CountAsync(c => c.Status == "active")`
   - Line 26: `_db.Customers.CountAsync(c => c.Status == "inactive")`
   - Line 28: `_db.Hubs.CountAsync(h => h.Status == "active")`
   - Line 29: `_db.Hubs.CountAsync(h => h.Status == "inactive")`
   - Line 30: `_db.Hubs.CountAsync(h => h.Status == "needs_reauth")`
   That is indeed 5 sequential COUNT queries, but across 2 different tables (Customers and Hubs), not just one.

2. **About page line numbers are wrong.** The issue cites `About.cshtml.cs` lines 48-50 for "3 sequential COUNT queries." The actual queries are at lines 31-33 in `src/Hpoll.Admin/Pages/About.cshtml.cs`:
   - Line 31: `_db.Customers.CountAsync()`
   - Line 32: `_db.Hubs.CountAsync()`
   - Line 33: `_db.Devices.CountAsync()`
   These count 3 entirely different tables (Customers, Hubs, Devices) with no filter predicates.

#### Assessment of Proposed Optimizations

**Dashboard GroupBy suggestion (partially valid but impractical):**
The suggestion to collapse the 5 COUNT queries into 2 GroupBy queries is technically possible. For Customers, you could do:
```csharp
var customerCounts = await _db.Customers
    .GroupBy(c => c.Status)
    .Select(g => new { Status = g.Key, Count = g.Count() })
    .ToDictionaryAsync(x => x.Status, x => x.Count);
```
And similarly for Hubs. This reduces 5 queries to 2. However, the resulting code is significantly more complex: you need to handle missing keys (what if there are zero inactive customers?), extract values from dictionaries, and the readability drops substantially. The current code is immediately clear about what each property represents.

**About page Task.WhenAll suggestion (flawed):**
The issue suggests parallelizing the 3 About page counts with `Task.WhenAll`, noting it "requires separate DbContext instances." This is technically correct that `DbContext` is not thread-safe, but the suggestion is impractical: injecting or creating 3 separate `DbContext` instances just to run 3 trivial counts in parallel adds significant complexity for negligible gain. The alternative suggestion of "a single raw SQL query" would work (`SELECT (SELECT COUNT(*) FROM Customers), (SELECT COUNT(*) FROM Hubs), (SELECT COUNT(*) FROM Devices)`) but introduces raw SQL into an otherwise clean EF Core codebase, which is a worse tradeoff.

**About page GroupBy is impossible:** As the prior reviewer noted, since the 3 counts span Customers, Hubs, and Devices -- three completely unrelated tables -- a GroupBy approach is impossible. There is no single table to group on.

#### Performance Reality Check

This is the critical point: **SQLite is an in-process database**. Every one of these COUNT queries executes as a function call within the same process -- there is no TCP connection, no network serialization, no connection pooling overhead. A `SELECT COUNT(*) FROM Customers WHERE Status = 'active'` on a table with likely tens to low hundreds of rows completes in single-digit microseconds.

Even with 5 sequential queries on the Dashboard, we are talking about total execution time well under 1 millisecond. This is an admin portal with minimal traffic (likely a single administrator). The page also performs 3 additional queries with JOINs (ExpiringTokenHubs, FailingHubs, RecentLogs at lines 32-50) that dwarf the COUNT query time.

Collapsing 5 queries to 2 would save approximately 10-50 microseconds on a page that likely takes 5-50 milliseconds to render end-to-end. This is a 0.1% improvement at best.

#### Recommendation

**Close as wontfix.** The current code is clear, readable, and maintainable. The proposed optimizations add complexity (GroupBy dictionaries, raw SQL, or multi-DbContext patterns) for an unmeasurable performance benefit on an admin page. This is a textbook case of premature optimization. The issue's factual errors (wrong line numbers for both files) further reduce confidence in the analysis.

### claude (independent critical review) — 2026-03-01

**Verdict: AGREE WITH WONTFIX -- issue is low-value with incorrect details; existing code is well-structured.**

I independently reviewed the source files on `main` and confirm the prior review's findings. Here is my own assessment:

#### Query Inventory

**Dashboard (`src/Hpoll.Admin/Pages/Index.cshtml.cs`)** executes 8 total DB queries in `OnGetAsync()`:
1. Lines 25-26: Two COUNT queries on `Customers` table (active/inactive status filters)
2. Lines 28-30: Three COUNT queries on `Hubs` table (active/inactive/needs_reauth status filters)
3. Lines 33-37: One query on `Hubs` with `Include(Customer)` for expiring tokens
4. Lines 39-43: One query on `Hubs` with `Include(Customer)` for failing hubs
5. Lines 45-50: One query on `PollingLogs` with `Include(Hub).ThenInclude(Customer)` for recent activity

The issue focuses on only the 5 COUNT queries (items 1-2), ignoring the 3 JOIN queries (items 3-5) that are far more expensive. This misplaced focus reveals the issue was generated without profiling.

**About (`src/Hpoll.Admin/Pages/About.cshtml.cs`)** executes 4 total DB queries:
1. Lines 31-33: Three COUNT queries on `Customers`, `Hubs`, and `Devices` (no filters, full table counts)
2. Lines 39-42: One query on `SystemInfo` with ordering

#### Can EF Core Actually Combine These?

The issue suggests GroupBy for the Dashboard counts. While this is syntactically possible for the per-table counts (e.g., group Customers by Status), EF Core's GroupBy translation has historically been one of its weakest areas. In EF Core 8 on SQLite, a `GroupBy(c => c.Status).Select(g => new { g.Key, Count = g.Count() })` will translate correctly, but the resulting C# code must then defensively handle missing dictionary keys (e.g., if no customers have status "inactive", that key is absent). This transforms 2 clean, self-documenting lines into ~8 lines of dictionary lookups with default values. The net effect is negative for maintainability.

For the About page, the 3 counts span 3 entirely unrelated tables (`Customers`, `Hubs`, `Devices`). No LINQ construct can combine these. The only options are:
- **Raw SQL**: `SELECT (SELECT COUNT(*) FROM Customers), (SELECT COUNT(*) FROM Hubs), (SELECT COUNT(*) FROM Devices)` -- works, but introduces raw SQL into an otherwise pure-EF codebase for negligible benefit.
- **Task.WhenAll with separate DbContext instances**: As noted, `DbContext` is not thread-safe. You would need to resolve 3 separate scoped `DbContext` instances from `IServiceProvider`, which is disproportionate complexity.

Neither option is warranted.

#### Performance Impact is Unmeasurable

Key facts that make this issue irrelevant in practice:

1. **SQLite is in-process.** There is zero network I/O. Each COUNT query is a function call within the same process address space. Typical execution time for `SELECT COUNT(*) FROM <table> WHERE Status = 'active'` on a table with tens of rows: **1-5 microseconds**.

2. **The tables are tiny.** This is a Hue monitoring service -- a deployment might have 1-10 customers, 1-50 hubs, and 1-500 devices. COUNT on these tables is virtually free. SQLite likely does not even need to scan rows; for unfiltered COUNTs it can use internal page counts.

3. **The admin portal is low-traffic.** The `[Authorize]` attribute (applied via convention for all admin pages) confirms this is behind authentication. It serves a single administrator or small ops team. Even if the page were 10x slower, no user would notice.

4. **The heavier queries dominate.** The 3 JOIN queries on the Dashboard (ExpiringTokenHubs, FailingHubs, RecentLogs) involve `Include`/`ThenInclude` with ordering and filtering. These will take 10-100x longer than the simple COUNTs. Optimizing the COUNTs while ignoring the JOINs is optimizing the wrong thing.

5. **Total estimated savings: 10-30 microseconds** out of a page load measured in milliseconds. This is below the noise floor of any benchmark.

#### Issue Quality Assessment

- **Line numbers incorrect** for both files (off by 3 for Dashboard, off by 17 for About), indicating the issue was auto-generated without verification against actual source.
- **GroupBy suggestion** is technically feasible for Dashboard but harms readability for no measurable gain.
- **Task.WhenAll suggestion** is correctly caveated about thread safety but still impractical -- the cure is worse than the disease.
- **Missing context**: The issue fails to mention the 3 heavier JOIN queries on the same Dashboard page, which would be far more impactful to optimize if performance were actually a concern.

#### Final Recommendation

**Close as wontfix.** The code is clean, idiomatic, and performs well. The proposed changes would add complexity without measurable benefit. If Dashboard performance ever becomes a concern, the focus should be on the JOIN queries and adding caching, not on collapsing trivial COUNTs.
