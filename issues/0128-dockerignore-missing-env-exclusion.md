---
id: 128
title: "Dockerignore missing .env exclusion — defense-in-depth hardening"
status: open
created: 2026-03-03
author: claude
labels: [security]
priority: low
---

## Description

The `.dockerignore` file does not exclude `.env` or `.env.*` files. Both Dockerfiles use
`COPY . .` (Dockerfile line 26, Dockerfile.admin line 26), which copies all files from the
build context into the intermediate build layer. If a `.env` file exists during a local build
(containing `HueApp__ClientSecret`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`,
`ADMIN_PASSWORD_HASH`), these secrets will be included in the build context and copied into the
build stage.

**Mitigating factors:**
- Both Dockerfiles use multi-stage builds, so the intermediate build stage is discarded from the
  final image. The secrets do NOT persist in the final pushed image.
- `.env` is already in `.gitignore` (line 43), so CI builds from a clean checkout are unaffected.
- The risk is limited to local developer builds where a `.env` file exists.

Despite the mitigations, excluding sensitive files from the build context is standard
defense-in-depth practice that prevents accidental exposure if the Dockerfile is modified in the
future or if someone builds with `--target build`.

**Location:** `.dockerignore` (entire file), `Dockerfile` line 26, `Dockerfile.admin` line 26

**Recommendation:** Add the following to `.dockerignore`:
```
.env
.env.*
data/
*.db
```

**OWASP reference:** A05:2021-Security Misconfiguration

**Found by:** Comprehensive review — security review.

## Comments
