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

### claude (critical review) — 2026-03-01

**Verdict: PARTIALLY_VALID — The duplication is real but the proposed solution is flawed.**

#### Verification of Claims

**1. Number of occurrences:** The issue claims 5 occurrences. The existing review comment claims 7. The actual count of handlers that call `_db.Customers.Include(c => c.Hubs).FirstOrDefaultAsync(c => c.Id == id)` is **7** (at lines 59, 84, 106, 126, 164, 203, 215 in `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs`):

| Handler | Line | Method |
|---------|------|--------|
| `OnGetAsync` | 59-63 | GET handler |
| `OnPostUpdateNameAsync` | 84-85 | POST |
| `OnPostUpdateEmailsAsync` | 106-107 | POST |
| `OnPostUpdateSendTimesAsync` | 126-127 | POST |
| `OnPostUpdateTimeZoneAsync` | 164-165 | POST |
| `OnPostToggleStatusAsync` | 203 | POST |
| `OnPostRegisterHubAsync` | 215-216 | POST |

**2. Line numbers in the issue are inaccurate.** The issue cites lines 48-57, 70-75, 91-97, 110-114, 140-146. None of these correspond to the actual customer-loading code in the file.

**3. The hydration is NOT uniform across handlers — this is the critical flaw in the recommendation.** Each handler deliberately sets a *different subset* of bind properties:

| Handler | Sets Customer | EditEmail | EditName | EditCcEmails | EditBccEmails | EditSendTimesLocal | EditTimeZoneId | DefaultSendTimesDisplay |
|---------|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `OnGetAsync` (line 64-70) | Y | Y | Y | Y | Y | Y | Y | Y |
| `OnPostUpdateNameAsync` (line 86-89) | Y | Y | — | Y | Y | — | — | — |
| `OnPostUpdateEmailsAsync` (line 108-109) | Y | — | Y | — | — | — | — | — |
| `OnPostUpdateSendTimesAsync` (line 128-133) | Y | Y | Y | Y | Y | — | — | Y |
| `OnPostUpdateTimeZoneAsync` (line 166-172) | Y | Y | Y | Y | Y | Y | — | Y |
| `OnPostToggleStatusAsync` (line 203-210) | — | — | — | — | — | — | — | — |
| `OnPostRegisterHubAsync` (line 217-221) | Y | Y | Y | Y | Y | — | — | — |

The key insight is that **each POST handler intentionally omits the bind property it is updating**. For example, `OnPostUpdateNameAsync` does NOT set `EditName` because that value comes from the form POST via `[BindProperty]`. If a naive `LoadCustomerAsync` helper populated all fields including `EditName`, it would overwrite the user's submitted value, breaking validation-error re-display. The same pattern applies to `OnPostUpdateEmailsAsync` (omits `EditEmail`, `EditCcEmails`, `EditBccEmails`) and so on.

`OnPostToggleStatusAsync` (line 201-211) is an outlier: it does not hydrate any bind properties at all because it redirects rather than returning `Page()`.

**4. The code snippet in the issue is misleading.** The issue presents a single monolithic block that it claims is repeated verbatim. In reality, no two handlers have the same hydration pattern. The DB query + null check (2 lines) is the only truly identical code.

#### What Would Actually Be Extractable

A helper could reasonably extract only the DB query and null check:

```csharp
private async Task<Customer?> FindCustomerAsync(int id)
{
    return await _db.Customers.Include(c => c.Hubs)
        .FirstOrDefaultAsync(c => c.Id == id);
}
```

This saves 1 line per handler (the `.Include(c => c.Hubs)` chain). The `null` check and `return NotFound()` cannot be extracted into a helper without changing the return type to something like a tuple or using an out parameter, since the handler needs to return `IActionResult` on null. A more sophisticated approach using a `LoadAndHydrate` method with flags or an enum to control which fields to set would likely add more complexity than it removes.

#### Assessment of Priority

The "low" priority is appropriate. This is a cosmetic code-quality concern. The existing code is correct, each handler's hydration is intentionally tailored, and the file is 351 lines — not excessive for a detail page with 7 handlers. The existing review comment on this issue already correctly identifies the core problems.

#### Conclusion

The issue correctly identifies that the DB query is repeated 7 times. However, it significantly mischaracterizes the duplication by claiming the *hydration boilerplate* is identical across handlers. The proposed `LoadCustomerAsync` helper that "populates all bind properties" would be incorrect — it would break form re-display on validation failure. The only safe extraction is a 3-line `FindCustomerAsync` method, which provides marginal benefit. Recommend closing as **wont-fix** or downgrading to a note for future reference rather than an actionable issue.
