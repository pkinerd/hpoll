---
id: 66
title: "Fix CLAUDE.md inaccuracies: CI trigger pattern and missing battery mention"
status: closed
closed: 2026-03-01
created: 2026-03-01
author: claude
labels: [documentation]
priority: low
---

## Description

CLAUDE.md has two minor inaccuracies identified during documentation review:

1. **CI trigger pattern:** CLAUDE.md states CI "runs on pushes to `main`, `dev`, and `claude/*` branches." The actual workflow (`.github/workflows/build-and-test.yml`) uses the pattern `claude/*-*` (requiring a hyphen), explicitly excludes `claude/zzsysissuesskill-*`, and also triggers on `pull_request` events and `workflow_dispatch`. The documented pattern is misleading.

2. **Project overview missing battery:** The overview says the service "polls Hue Bridge hubs for motion and temperature sensor data." Since the `AddBatteryPolling` migration, the service also polls device power/battery data. The overview should mention battery monitoring.

**Recommended fixes:**
- Update CI section to reflect actual trigger patterns including `claude/*-*` glob with exclusion, PR triggers, and manual dispatch
- Update overview to: "polls Hue Bridge hubs for motion, temperature, and battery sensor data"

**Source:** Comprehensive review -- documentation review findings 21-22

## Comments
