---
id: 67
title: "Entity default values use DateTime.UtcNow at instantiation, not persistence time"
status: closed
closed: 2026-03-01
created: 2026-03-01
author: claude
labels: [enhancement, code-quality]
priority: low
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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Priority downgraded medium->low. Practically irrelevant: entities are created and saved in the same flow. DeviceReading.Timestamp and PollingLog.Timestamp defaults are never relied upon (always overridden by pollTime). HasDefaultValueSql would not work as described (EF Core sends explicit values). SaveChanges interceptor would break PollingService's intentional Timestamp=pollTime.

### claude — 2026-03-01 (detailed critical review)

**Verdict: NOT_A_BUG. Recommend closing as won't-fix.**

#### Code path analysis

I traced every entity creation site in the codebase. Here is what actually happens:

1. **Customer** (`Create.cshtml.cs` line 64): `new Customer { ... }` is created, then `_db.Customers.Add(customer)` and `await _db.SaveChangesAsync()` immediately follow on lines 75-76. The gap between instantiation and persistence is a single synchronous computation of `NextSendTimeUtc` -- microseconds at most.

2. **Hub** (`OAuthCallback.cshtml.cs` line 117): `new Hub { ... }` is created and immediately added/saved on lines 128-129. The only code between construction and save is setting the object's own properties in the initializer. Sub-millisecond gap.

3. **Device** (`PollingService.cs` line 343, in `GetOrCreateDeviceAsync`): `new Device { ... }` is created and `SaveChangesAsync` is called immediately on line 350. No meaningful delay.

4. **DeviceReading** (`PollingService.cs` lines 166, 191, 217): These are created with `Timestamp = pollTime` explicitly set, completely overriding the `DateTime.UtcNow` default. The default initializer value is never used. `SaveChangesAsync` happens in the `finally` block of `PollHubAsync` after all readings for a hub are added.

5. **PollingLog** (`PollingService.cs` line 113): Created as `new PollingLog { HubId = hub.Id, Timestamp = pollTime }` -- again, `Timestamp` is explicitly set to `pollTime`, overriding the default. Saved in the same `finally` block.

In every single case, either (a) persistence happens immediately after construction with negligible delay, or (b) the timestamp property is explicitly set, making the default irrelevant.

#### The recommended fixes would cause harm

The issue recommends two alternatives:

1. **`HasDefaultValueSql("datetime('now')")`**: This does not work as the issue implies. EF Core only uses `HasDefaultValueSql` when the property value is the CLR default (i.e., `DateTime.MinValue` for `DateTime`). Since every entity sets `DateTime.UtcNow` in the property initializer, EF Core sees a non-default value and always sends it in the INSERT statement, bypassing the SQL default entirely. To make `HasDefaultValueSql` work, you would need to *remove* the C# default and use `ValueGeneratedOnAdd()`, which means the property would be `DateTime.MinValue` in the C# object until after `SaveChanges` completes. This would break any code that reads `CreatedAt` before saving (including logging and any pre-save validation).

2. **SaveChanges override / interceptor**: The `PollingService` intentionally sets `Timestamp = pollTime` on `DeviceReading` and `PollingLog` entities so that all readings from a single poll cycle share the exact same timestamp. A `SaveChanges` interceptor that overwrites timestamp fields would destroy this deliberate design choice. You would need complex logic to distinguish "this timestamp should be auto-set" from "this timestamp was intentionally set by the caller," which adds significant complexity for zero practical benefit.

#### Assessment of claimed impact

The issue claims "subtle timing bugs in tests and edge cases where entities are created well before they are persisted." However:

- There is no code path where an entity is created and then persisted significantly later. The create-and-save pattern is immediate in all cases.
- In tests, `DateTime.UtcNow` in defaults is standard practice for EF Core entities. Test assertions that need precise timestamps should (and do) set them explicitly.
- The "edge case" of objects created long before `SaveChanges` is purely hypothetical and does not exist in this codebase.

#### Conclusion

This issue describes a theoretically valid concern about a general EF Core pattern, but it does not manifest as a real problem in the hpoll codebase. The two high-volume entity types (`DeviceReading` and `PollingLog`) always have their timestamps explicitly set. The low-volume entity types (`Customer`, `Hub`, `Device`) are created and immediately persisted. Both recommended fixes would either not work as described or would introduce real regressions. The current code is correct as-is, and modifying it would add complexity without benefit.

Recommend closing this issue as won't-fix.

### claude — 2026-03-01

Closing: Wontfix: proposed fixes would break code — SaveChanges interceptor would override PollingService's intentional Timestamp=pollTime
