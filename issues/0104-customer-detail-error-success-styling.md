---
id: 104
title: "Customer Detail error message displayed with success styling"
status: closed
created: 2026-03-02
author: claude
labels: [bug]
priority: low
closed: 2026-03-03
---

## Description

In `Customers/Detail.cshtml.cs` (line 232), when HueApp configuration is missing, the code sets:

```csharp
SuccessMessage = "HueApp:ClientId and HueApp:CallbackUrl must be configured.";
```

This is clearly an **error message** being placed in a field named `SuccessMessage`, which renders with success styling (green alert box) in the Razor view. The user sees a configuration error displayed as if it were a successful operation.

**Location:** `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs`, line 232

**Recommendation:**
Add a separate `ErrorMessage` property (like `Hubs/Detail` already has) and use it for error conditions. Alternatively, use `ModelState.AddModelError` to display configuration errors with appropriate error styling.

## Comments
