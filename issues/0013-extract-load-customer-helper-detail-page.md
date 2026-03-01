---
id: 13
title: "Extract shared LoadCustomerAsync helper in Detail page model"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, code-quality]
priority: low
---

## Description

In `Customers/Detail.cshtml.cs`, every POST handler repeats the same customer-loading and field-hydration boilerplate (5 occurrences at lines 48-57, 70-75, 91-97, 110-114, 140-146):

```csharp
var customer = await _db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id);
if (customer == null) return NotFound();
Customer = customer;
EditEmail = customer.Email;
EditName = customer.Name;
EditCcEmails = customer.CcEmails;
EditBccEmails = customer.BccEmails;
```

**Recommendation:** Extract a private `LoadCustomerAsync(int id)` helper that returns `Customer?` and populates all bind properties.

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. There are **7 handlers** (not 5) that load a customer. The hydration is NOT identical across handlers -- each POST handler deliberately skips fields being updated via [BindProperty]. A naive helper that sets all fields would break validation error re-display. Line numbers significantly off. The most honest refactor extracts only the DB query + null check (~2 lines saved per handler).
