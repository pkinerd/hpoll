# Issue File Schema

Issue files are stored in `issues/` and named `<id>-<slug>.md`.

## Frontmatter

```yaml
---
id: 1
title: "Issue title"
status: open          # open | in-progress | closed
created: 2026-02-26
author: claude
labels: [feature, planning]  # optional
priority: medium              # optional: low | medium | high | critical
closed: 2026-02-26           # optional: only when status is closed
---
```

## Body

```markdown
## Description

Description of the issue.

## Comments

### author — YYYY-MM-DD

Comment text.
```
