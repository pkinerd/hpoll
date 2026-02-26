# Issues Branch Guide

This branch tracks project issues using markdown files.

## Structure

- `state.json` — Tracks the next issue ID and valid labels
- `INDEX.md` — Table of all issues with summary info
- `issues/` — Individual issue files named `<id>-<slug>.md`
- `docs/` — Project documentation files
- `SCHEMA.md` — Format specification for issue files

## Conventions

- Issue IDs are zero-padded to 4 digits (e.g., `0001`)
- Slugs are lowercase, hyphen-separated versions of the title
- All issue files use YAML frontmatter
- Comments are appended under the `## Comments` section
