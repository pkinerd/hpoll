---
id: 135
title: "README settings table does not include non-.NET env vars like ADMIN_PASSWORD_HASH"
status: open
created: 2026-03-03
author: claude
labels: [documentation]
priority: low
---

## Description

The settings reference table in `README.md` (lines 27-56) exclusively documents settings that go
through the .NET configuration system (`IConfiguration` / Options pattern), using the
`Section:Key` and `Section__Key` naming convention. Environment variables that are read directly
(not through .NET configuration) are absent from this table, including:

- `ADMIN_PASSWORD_HASH` — read via `Environment.GetEnvironmentVariable()` in `Program.cs`
- `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` — consumed directly by the AWS SDK

`ADMIN_PASSWORD_HASH` is well-documented elsewhere in the README: in the full `docker-compose.yml`
example (line 224), the `docker run` example (line 255), and its own dedicated "Admin panel
password" section (lines 324-338). It also appears in `.env.example`.

**Recommendation:** Consider one of:
1. Add a small secondary table after the main settings table titled "Other environment variables"
   listing `ADMIN_PASSWORD_HASH` and AWS credentials
2. Add a note after the settings table pointing readers to the relevant sections
3. Leave as-is — the existing dedicated sections and examples already provide adequate
   documentation for these variables

**Found by:** Comprehensive review — documentation review.

## Comments
