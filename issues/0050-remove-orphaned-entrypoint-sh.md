---
id: 50
title: "Remove or fix orphaned entrypoint.sh"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [bug]
priority: low
---

## Description

**Severity: Low**

`entrypoint.sh` (lines 6-8) references a user named `hpoll`:
```bash
chown -R hpoll:hpoll /app/data
exec runuser -u hpoll -- dotnet Hpoll.Worker.dll
```

Both Dockerfiles (`Dockerfile` line 32, `Dockerfile.admin` line 32) create a user named `appuser`, not `hpoll`. Neither Dockerfile references `entrypoint.sh` — they use direct `ENTRYPOINT ["dotnet", ...]`.

The script would fail if anyone attempted to use it, and its presence in the repository could confuse developers.

**Remediation:** Either delete `entrypoint.sh` entirely, or update it to use `appuser` and reference it from the Dockerfiles if a custom entrypoint is desired.

## Comments
