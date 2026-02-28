---
id: 11
title: "Extract DeviceReading JSON parsing into typed accessor methods"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: medium
---

## Description

The `DeviceReading.Value` field stores JSON strings, but every consumer performs inline `JsonDocument.Parse()` with identical try/catch patterns. The same motion-parsing lambda appears 4 times, the temperature-parsing lambda 2 times, and battery parsing 1 time.

**Locations:**
- `EmailRenderer.cs` lines 87-89, 96-98 (motion x2), 103-104 (temp), 144-146 (battery)
- `Detail.cshtml.cs` lines 214-216, 223-225 (motion x2), 229-231 (temp)

**Recommendation:** Add a `ReadingParser` utility class or extension methods on `DeviceReading`:
```csharp
public static bool ParseMotion(string json) { ... }
public static double? ParseTemperature(string json) { ... }
public static (int? Level, string? State) ParseBattery(string json) { ... }
```
This eliminates duplicate parsing, reduces per-window JSON parse count from 14 to 7, and encapsulates the JSON schema in one place.

## Comments
