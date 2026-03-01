---
id: 67
title: "Entity default values use DateTime.UtcNow at instantiation, not persistence time"
status: open
created: 2026-03-01
author: claude
labels: [enhancement, code-quality]
priority: medium
---

## Description

All entity classes (`Customer`, `Device`, `DeviceReading`, `Hub`, `PollingLog`) use default property initializers like `public DateTime CreatedAt { get; set; } = DateTime.UtcNow;`. This captures the time when the object is **instantiated** (which could be deserialization time, test setup time, etc.), not when it is saved to the database.

**Locations:**
- `src/Hpoll.Data/Entities/Customer.cs` lines 12-13
- `src/Hpoll.Data/Entities/Device.cs` lines 12-13
- `src/Hpoll.Data/Entities/DeviceReading.cs` line 8
- `src/Hpoll.Data/Entities/Hub.cs` lines 18-19
- `src/Hpoll.Data/Entities/PollingLog.cs` line 8

**Impact:** Subtle timing bugs in tests and edge cases where entities are created well before they are persisted.

**Recommendation:** Either use EF Core's `HasDefaultValueSql("datetime('now')")` in the model builder, or set timestamps explicitly in a `SaveChanges` override / interceptor.

*Found during comprehensive review (code quality review).*

## Comments
