---
id: 86
title: "Hue API error response bodies logged with potential sensitive data"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: low
---

## Description

When Hue API calls fail, `HueApiClient` logs the error response body (truncated to 500 chars)
at Warning level in multiple locations (lines 96-97, 120-121, 169-170, 200-201). Additionally,
line 141 logs the full JSON response on unexpected registration format.

These error responses could contain:
- Token values or internal API details from the Hue service
- Information useful for reconnaissance if logs are exposed

This is distinct from #64 (sanitize exception messages in PollingLog) — this finding is about
direct Warning-level log output in the HueApiClient service.

**Found by:** Comprehensive review — security review.

**OWASP reference:** A09:2021-Security Logging and Monitoring Failures

**Recommendation:** Log only HTTP status codes and generic messages at Warning level. Gate
response body logging behind `Debug` log level. Redact potential token or credential values.

## Comments
