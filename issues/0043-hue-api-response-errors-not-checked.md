---
id: 43
title: "Hue API response errors array is never checked after deserialization"
status: open
created: 2026-02-28
author: claude
labels: [bug]
priority: medium
---

## Description

**Severity: Medium**

In `HueApiClient.cs` `GetResourceAsync` (line 179), the deserialized `HueResponse<T>` contains an `Errors` list that is never inspected. Per the Hue CLIP API v2 error handling documentation, even HTTP 200 responses can contain errors in the `errors` array, and HTTP 207 responses indicate partial success with some operations failing.

The current code only checks `response.IsSuccessStatusCode` and then returns the deserialized response without validation:

```csharp
var result = JsonSerializer.Deserialize<HueResponse<T>>(json, JsonOptions);
return result ?? throw new InvalidOperationException(...);
// result.Errors is never checked
```

**Impact:** Partial API failures or warnings from the Hue Bridge are silently ignored. Motion/temperature data could be incomplete without any indication in logs.

**Remediation:** After deserialization, check `if (result.Errors.Count > 0)` and log them. For critical endpoints, consider throwing on errors.

## Comments
