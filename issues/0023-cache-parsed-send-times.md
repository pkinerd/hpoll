---
id: 23
title: "Cache parsed SendTimesUtc in EmailSchedulerService constructor"
status: open
created: 2026-02-28
author: claude
labels: [enhancement, performance]
priority: low
---

## Description

`EmailSchedulerService.GetNextSendTime` (lines 66-95) re-parses the `SendTimesUtc` string list, re-creates a `List<TimeSpan>`, and re-sorts it on every invocation. Since these values come from configuration and don't change at runtime, they should be parsed once in the constructor.

Minor impact (runs a few times per day), but it's unnecessary repeated work.

## Comments
