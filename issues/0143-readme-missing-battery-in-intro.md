---
id: 143
title: "README introduction omits battery sensor data"

closed: 2026-03-15
created: 2026-03-04
author: claude
labels: [documentation]
priority: low
---

## Description

The README introduction (line 3-5) says:

> "It periodically polls Hue Bridge hubs for motion and temperature sensor data"

This omits battery data, which the system also polls. The CLAUDE.md correctly says "motion, temperature, and battery sensor data."

**Recommendation:** Update README line 4 to: "...for motion, temperature, and battery sensor data..."

**Found by:** Comprehensive review — documentation review.

## Comments

### critical-review — 2026-03-04

Critical review: CONFIRM. Finding is valid. Battery is a first-class data type confirmed in code and config but missing from README intro sentence.
