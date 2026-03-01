---
id: 61
title: "Admin portal Docker container binds to all network interfaces"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: low
---

## Description

The admin portal Docker container listens on `http://+:8080`, binding to all network interfaces. The `docker-compose.yml` maps `8080:8080` without restricting to localhost:

```yaml
ports:
  - "8080:8080"  # Accessible on all host interfaces
```

If the Docker host is directly reachable from the network (no firewall or reverse proxy), the admin portal is exposed to all traffic.

**Files:**
- `Dockerfile.admin:36` (`ASPNETCORE_URLS=http://+:8080`)
- `docker-compose.yml` port mapping

**Recommended fix:** Document that a reverse proxy with TLS is required for production. Consider changing the default `docker-compose.yml` to bind to localhost: `"127.0.0.1:8080:8080"`, or add a commented example showing the secure configuration.

**Source:** Security review finding S6.3

## Comments
