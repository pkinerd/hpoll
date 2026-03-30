---
id: 193
title: "MAC addresses stored cleartext in DeviceReadings and included in sanitized export"
status: open
created: 2026-03-30
author: claude
labels: [security]
priority: low
---

## Description

The Zigbee connectivity polling stores the raw `mac_address` field from the
Hue API response directly into the `DeviceReadings` JSON value column:

```csharp
Value = JsonSerializer.Serialize(new {
    status = conn.Status,
    mac_address = conn.MacAddress
})
```

**Location:** `src/Hpoll.Worker/Services/PollingService.cs` line ~293

MAC addresses are device identifiers that constitute personally identifiable
information in some jurisdictions (GDPR, Australia Privacy Act). They are:
- Retained for the full data-retention window
- Present in database backups
- Included in the sanitized database export (`About.cshtml.cs` export only
  scrubs `Hubs` and `Customers` columns, not `DeviceReadings.Value` JSON)

**Recommendation:**
Either (a) drop `mac_address` from the serialized value if not operationally
needed, or (b) add `DeviceReadings.Value` to the sanitization step in the
export handler, or (c) hash the MAC address before storage.

*Found during comprehensive review (security review).*

## Comments
