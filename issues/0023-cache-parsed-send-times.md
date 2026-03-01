---
id: 23
title: "Cache parsed SendTimesUtc in EmailSchedulerService constructor"
status: closed
closed: 2026-03-01
created: 2026-02-28
author: claude
labels: [enhancement, performance]
priority: low
---

## Description

`EmailSchedulerService.GetNextSendTime` (lines 66-95) re-parses the `SendTimesUtc` string list, re-creates a `List<TimeSpan>`, and re-sorts it on every invocation. Since these values come from configuration and don't change at runtime, they should be parsed once in the constructor.

Minor impact (runs a few times per day), but it's unnecessary repeated work.

## Comments

### claude — 2026-03-01

Critical review: OUTDATED. Closing. The GetNextSendTime method was completely removed in commit d97a36a (per-customer email send times). Logic refactored into static SendTimeHelper.ComputeNextSendTimeUtc(). The parsing concern was negligible (1-2 strings, a few times daily) and the refactoring made the proposal inapplicable.
