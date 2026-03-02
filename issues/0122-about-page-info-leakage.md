---
id: 122
title: "About page exposes detailed system and deployment information"
status: open
created: 2026-03-02
author: claude
labels: [security]
priority: low
---

## Description

The About page (`src/Hpoll.Admin/Pages/About.cshtml.cs`, lines 29-104) displays system information including the .NET runtime version, OS version, machine name, hostname, data path, build branch, commit hash, polling configuration, email from-address, and AWS region. The page is properly protected behind authentication (global `RequireAuthorization()` in Program.cs, line 105).

The Worker stores operational metadata including `email.from_address` and `email.aws_region` in the SystemInfo table, which is displayed on this page. No secrets, credentials, or API keys are exposed — only configuration metadata.

This is a minor defense-in-depth consideration, not a vulnerability. The About page serves a legitimate operational purpose for administrators troubleshooting deployment issues.

**Category:** config / information disclosure
**Severity:** low
**Found by:** Security review (comprehensive review 2026-03-02)
**OWASP Reference:** A05:2021-Security Misconfiguration

### Recommendation

Ensure the About page remains behind authentication. Consider whether displaying the full filesystem data path is necessary, as it reveals container layout details. All other displayed values (runtime version, build info, polling config, email from-address, AWS region) are operational metadata appropriate for an admin dashboard. In Dockerized deployments, machine name/hostname typically reveal only a container ID.

## Comments
