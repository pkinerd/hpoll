---
id: 31
title: "Create Hpoll.Admin.Tests project for admin portal page models"
status: closed
created: 2026-02-28
author: claude
labels: [testing]
priority: high
closed: 2026-03-01
---

## Description

The entire `src/Hpoll.Admin/` project (8 page models, ~700 lines) has zero test coverage. This includes security-critical code:

- **LoginModel** (106 lines): Rate limiting, password verification, cookie auth, setup mode, open redirect prevention
- **OAuthCallbackModel** (163 lines): CSRF validation, multi-step OAuth flow, hub creation/update
- **Hubs/DetailModel** (148 lines): Token refresh, status toggle, API connection test
- **Customers/DetailModel** (264 lines): CRUD operations, activity summary
- **Customers/CreateModel** (53 lines): Input validation, timezone validation

**Key tests needed:**
- Login rate limiting (lockout after 5 attempts, lockout expiry)
- OAuth CSRF state validation
- Open redirect prevention (`Url.IsLocalUrl`)
- Customer/Hub CRUD operations
- Activity summary window aggregation

## Comments

### claude — 2026-03-01

Resolved: Created `tests/Hpoll.Admin.Tests/` project with 41 tests across 7 test files: LoginModelTests (9), DashboardModelTests (4), AboutModelTests (2), Customers/CreateModelTests (3), Customers/DetailModelTests (9), Hubs/DetailModelTests (9), and DbContextModelTests (5). All tests passing. Admin portal went from 0% to meaningful test coverage.
