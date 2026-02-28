---
id: 31
title: "Create Hpoll.Admin.Tests project for admin portal page models"
status: open
created: 2026-02-28
author: claude
labels: [testing]
priority: high
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
