---
id: 114
title: "Email and Data tests placed in unrelated test projects"
status: closed
closed: 2026-03-03
created: 2026-03-02
author: claude
labels: [testing, code-quality]
priority: low
---

## Description

Tests for `Hpoll.Email` classes are located in `Hpoll.Core.Tests`, and a `Hpoll.Data` schema test is in `Hpoll.Admin.Tests`. No dedicated `Hpoll.Email.Tests` or `Hpoll.Data.Tests` projects exist.

**Current placement:**
- `tests/Hpoll.Core.Tests/EmailRendererTests.cs` → tests `Hpoll.Email.EmailRenderer`
- `tests/Hpoll.Core.Tests/SesEmailSenderTests.cs` → tests `Hpoll.Email.SesEmailSender`
- `tests/Hpoll.Core.Tests/HubExtensionsTests.cs` → tests `Hpoll.Data.Entities.HubExtensions` (defensible: bridges Core and Data types)
- `tests/Hpoll.Admin.Tests/DbContextModelTests.cs` → tests `Hpoll.Data.HpollDbContext` (most egregious: pure DB schema test in Admin)

**Root cause:** No `Hpoll.Email.Tests` or `Hpoll.Data.Tests` projects exist. `Hpoll.Core.Tests` has project references to both `Hpoll.Email` and `Hpoll.Data`, so it became the catch-all location.

**Problem:**
- Creates confusion about where to find/add tests for Email and Data classes
- Makes it harder to measure per-project coverage accurately
- `DbContextModelTests` in `Admin.Tests` is particularly misplaced — pure database schema/constraint tests have no logical connection to the Admin web layer

**Recommendation:**
Create `Hpoll.Email.Tests` and move `EmailRendererTests.cs` and `SesEmailSenderTests.cs` there. The `HubExtensions` placement is more defensible since it bridges Core and Data types. Consider moving `DbContextModelTests` to a `Hpoll.Data.Tests` project if the Data layer grows. Low priority since tests exist and provide good coverage — this is organizational only.

## Comments

### claude — 2026-03-02

Skipping: Moving tests between projects is a structural reorganization that doesn't improve test quality or coverage. The existing tests work correctly in their current locations despite the organizational inconsistency.

### claude — 2026-03-03

Closing as won't-fix. Moving tests between projects is a structural reorganization that doesn't improve test quality, coverage, or reliability. The existing tests work correctly in their current locations. EmailRendererTests and SesEmailSenderTests in Hpoll.Core.Tests have proper project references to Hpoll.Email. DbContextModelTests in Hpoll.Admin.Tests has proper references to Hpoll.Data. Creating dedicated test projects would add build complexity without functional benefit.
