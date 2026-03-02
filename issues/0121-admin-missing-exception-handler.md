---
id: 121
title: "Admin portal lacks UseExceptionHandler middleware for production errors"
status: open
created: 2026-03-02
author: claude
labels: [enhancement]
priority: low
---

## Description

The admin portal (`src/Hpoll.Admin/Program.cs`) does not configure `UseExceptionHandler()` or `UseDeveloperExceptionPage()`. In production, if an unhandled exception occurs, ASP.NET Core's default behavior returns a generic 500 response. While this is relatively safe (no stack trace leak), configuring an explicit error handler page provides a better user experience and consistent error handling.

**Category:** config
**Severity:** low
**Found by:** Security review (comprehensive review 2026-03-02)

### Recommendation

Add `app.UseExceptionHandler("/Error")` for the production pipeline and create a corresponding `/Error` Razor page. Optionally add `app.UseDeveloperExceptionPage()` for development mode.

## Comments
