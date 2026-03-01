---
id: 61
title: "Admin portal Docker container binds to all network interfaces"
status: closed
closed: 2026-03-01
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

### claude — 2026-03-01

Critical review: PARTIALLY_VALID. Recommend relabeling security->documentation. Binding to 0.0.0.0 inside a container is standard and required Docker practice. The concern is solely the docker-compose port mapping, not the Dockerfile ENV. Admin portal requires authentication, has hardened cookies, runs as non-root. Synology NAS support (PUID/PGID) indicates LAN access is an intended use case.

### Critical Review — 2026-03-01

**Verdict: MOSTLY_INVALID — not a security issue, at best a minor documentation suggestion.**

#### 1. Binding to 0.0.0.0 inside a Docker container is standard, required behavior

The issue conflates two distinct things: the bind address *inside* the container
(`ASPNETCORE_URLS=http://+:8080` in `Dockerfile.admin`) and the port mapping on
the *host* (`"8080:8080"` in `docker-compose.yml`).

Inside a Docker container, binding to `0.0.0.0` (or `+` in ASP.NET Core) is
**mandatory** for the application to be reachable from outside the container.
Docker networking works by forwarding traffic from the host's network stack into
the container's isolated network namespace. If the application bound to
`127.0.0.1` inside the container, it would only be reachable from within the
container itself — making the entire container useless. This is not a
misconfiguration; it is the universally documented best practice for
containerized web applications. The `Dockerfile.admin` line cited by this issue
(`ASPNETCORE_URLS=http://+:8080`) is correct and should not be changed.

#### 2. The docker-compose port mapping is a reasonable default

The `docker-compose.yml` maps `"8080:8080"`, which exposes port 8080 on all host
interfaces. The issue suggests changing this to `"127.0.0.1:8080:8080"`.

However, binding to `127.0.0.1` on the host would make the admin portal
unreachable from any other machine on the network, which defeats the purpose for
the primary deployment target. The project explicitly supports Synology NAS
deployments (the `PUID`/`PGID` variables in `docker-compose.yml` are a Synology
convention), where users access the admin portal from other devices on their LAN.
Binding to localhost-only by default would break this use case and generate
confusion.

The existing `docker-compose.yml` is a development/quick-start example. Users
deploying in production are expected to configure networking appropriate to their
environment.

#### 3. The admin portal already has substantial security hardening

The issue implies the exposed portal is unprotected, but the codebase shows
significant security measures already in place:

- **Authentication required on all pages**: `app.MapRazorPages().RequireAuthorization()`
  ensures every page requires login. Only `/Login` and `/Logout` are anonymous.
- **Password hashing**: Uses ASP.NET Core's `PasswordHasher<T>` (PBKDF2-SHA256
  with random salt). Minimum 8-character password enforced.
- **HttpOnly, SameSite cookies**: Both auth and session cookies are configured
  with `HttpOnly = true`, `SameSite = Lax`, and `SecurePolicy = Always`.
- **Anti-forgery tokens**: Enabled on all forms.
- **Non-root container user**: The container runs as `appuser`, not root.
- **Forwarded headers support**: `UseForwardedHeaders()` is configured for
  reverse proxy deployments, showing this scenario was already considered.

#### 4. The recommended fix is already partially addressed

The README already documents the production deployment pattern with a reverse
proxy. The `CallbackUrl` configuration table shows:

| Environment | CallbackUrl value |
|---|---|
| Local development | `http://localhost:8080/Hubs/OAuthCallback` |
| Production (reverse proxy) | `https://admin.example.com/Hubs/OAuthCallback` |

The `ForwardedHeaders` middleware configuration in `Program.cs` further confirms
that reverse proxy deployment is an expected and supported pattern.

#### 5. Recommendation

This issue should be **closed as not a bug**. The behavior described is standard
Docker practice, not a security vulnerability. The admin portal is already
protected by authentication, hardened cookies, and anti-forgery tokens. The
project already documents reverse proxy usage for production.

If any action is desired, a single comment in `docker-compose.yml` noting that
users can restrict the bind address (e.g., `# Use "127.0.0.1:8080:8080" to
restrict to localhost`) would be sufficient, but this is a documentation
nicety, not a security fix. Relabeling from `security` to `documentation` and
lowering priority to `trivial` would be appropriate if the issue is kept open.

### claude — 2026-03-01

Closing: Wontfix: Docker standard behavior — binding 0.0.0.0 inside a container is required Docker practice
