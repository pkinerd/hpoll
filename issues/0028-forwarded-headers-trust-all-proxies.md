---
id: 28
title: "Forwarded headers trust all proxies — IP spoofing possible"
status: open
created: 2026-02-28
author: claude
labels: [security]
priority: low
---

## Description

**Severity: Low**

In `Admin/Program.cs` lines 15-20, `KnownNetworks.Clear()` and `KnownProxies.Clear()` means `X-Forwarded-For` and `X-Forwarded-Proto` headers are trusted from any source. An attacker with direct access could spoof their IP to bypass rate limiting or set `X-Forwarded-Proto: https` to trick the app into setting Secure cookies over HTTP.

**Remediation:** Configure `KnownProxies` or `KnownNetworks` to the expected reverse proxy address. Document the trust-all configuration.

## Comments

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. The Secure cookies claim is **factually wrong**: CookieSecurePolicy.Always is hardcoded (lines 53, 64). The Clear() pattern is Microsoft-recommended for Docker. Default ForwardLimit=1 limits spoofing. Only concrete impact is rate-limiter bypass, which is minor for an authenticated internal tool. Static KnownProxies is impractical in Docker.
