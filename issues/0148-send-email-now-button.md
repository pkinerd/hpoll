---
id: 148
title: "Add 'Send Email Now' button to Customer Detail page"
status: open
created: 2026-03-15
author: claude
labels: [feature, enhancement]
priority: medium
---

## Description

There is currently no way to manually trigger an email from the Admin portal. Emails are sent exclusively by the Worker's `EmailSchedulerService` on a time-based schedule (via `NextSendTimeUtc`). A "Send Email Now" button on the Customer Detail page would let admins trigger an ad-hoc email for testing or urgent delivery.

### Architecture Context

- Admin and Worker are **separate processes** sharing only a SQLite database (WAL mode)
- Admin references `Hpoll.Core` + `Hpoll.Data` but **not** `Hpoll.Email` or `Hpoll.Worker`
- Email sending requires `IEmailRenderer` (DB queries + HTML rendering) and `IEmailSender` (AWS SES)
- Worker polls the DB every ~1 minute looking for customers where `NextSendTimeUtc <= now`

### Implementation Options

#### Option A: Direct sending from Admin

Add `Hpoll.Email` as a project reference to Admin, register email services (`IEmailRenderer`, `IEmailSender`, `IAmazonSimpleEmailService`), and send inline during the HTTP POST via a new `OnPostSendEmailNowAsync` handler.

| Aspect | Detail |
|--------|--------|
| **Feedback** | Immediate — success/error shown on page reload |
| **Scheduled send** | Preserved (does not touch `NextSendTimeUtc`) |
| **Dependencies** | Admin gains `Hpoll.Email` + `AWSSDK.SimpleEmail`; needs AWS creds at runtime |
| **Migration** | None |
| **Complexity** | Medium — ~4 files changed, DI registrations, extract shared `ParseEmailList` logic |
| **Risk** | Admin deployment must now include AWS SES credentials; breaks current separation |

**Files to change:**
- `src/Hpoll.Admin/Hpoll.Admin.csproj` — add Hpoll.Email reference
- `src/Hpoll.Admin/Program.cs` — register `IEmailRenderer`, `IEmailSender`, `IAmazonSimpleEmailService`
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs` — inject email services, add `OnPostSendEmailNowAsync`
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml` — add button form
- `tests/Hpoll.Admin.Tests/Customers/DetailModelTests.cs` — add tests

#### Option B: Set `NextSendTimeUtc` to now (signal via DB)

Simply set `NextSendTimeUtc = DateTime.UtcNow`. Worker picks it up within ~1 minute via its normal loop.

| Aspect | Detail |
|--------|--------|
| **Feedback** | Delayed (~1 min); no send confirmation; "Email queued" message only |
| **Scheduled send** | **Consumed** — Worker advances `NextSendTimeUtc` to next scheduled time after sending |
| **Dependencies** | None new |
| **Migration** | None |
| **Complexity** | Low — ~20 lines of handler code + button in view |
| **Risk** | Overloads `NextSendTimeUtc` semantics; if Worker is down, email queues silently; no audit trail |

**Files to change:**
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs` — add handler (~20 lines)
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml` — add button form

#### Option C: Database flag + Worker pickup

Add `SendEmailRequested` boolean to `Customer`. Admin sets it to `true`; Worker checks for it alongside `NextSendTimeUtc`, sends, clears the flag, and does **not** advance `NextSendTimeUtc`.

| Aspect | Detail |
|--------|--------|
| **Feedback** | Delayed (~1 min); no send confirmation; "Email requested" message |
| **Scheduled send** | Preserved (Worker handles flag-triggered sends separately) |
| **Dependencies** | None new |
| **Migration** | Yes — add column to Customer table |
| **Complexity** | Medium-High — migration, Worker query changes, both Admin + Worker tests |
| **Risk** | Migration coordination (Worker must migrate first); stale requests if Worker is down for hours |

**Files to change:**
- `src/Hpoll.Data/Entities/Customer.cs` — add `SendEmailRequested` property
- New EF Core migration
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml.cs` — add handler
- `src/Hpoll.Admin/Pages/Customers/Detail.cshtml` — add button form + pending indicator
- `src/Hpoll.Worker/Services/EmailSchedulerService.cs` — modify query to check flag, clear after send
- Tests in both Admin and Worker

### Comparison

| Criterion | A (Direct) | B (Set time) | C (DB flag) |
|-----------|-----------|-------------|-------------|
| Immediate feedback | Yes | No | No |
| Preserves schedule | Yes | **No** | Yes |
| New Admin dependencies | **Yes** | No | No |
| Migration needed | No | No | **Yes** |
| Code changes | Medium | Small | Medium-Large |
| Deployment complexity | Higher | None | Moderate |

## Comments
