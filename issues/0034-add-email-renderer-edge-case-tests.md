---
id: 34
title: "Add missing edge case tests for EmailRenderer"
status: open
created: 2026-02-28
author: claude
labels: [testing]
priority: medium
---

## Description

Missing test scenarios for `EmailRenderer`:

- Invalid customer ID (nonexistent) — should produce empty HTML
- Invalid timezone — `TimeZoneNotFoundException` propagation
- XSS in device names — verify `HtmlEncode` works for `<script>` tags
- DST boundary crossing — readings during DST changeover hour
- Multiple hubs per customer — verify aggregation across hubs
- Reading at exact bucket boundary timestamp
- Battery reading with malformed JSON
- CancellationToken mid-render

## Comments
