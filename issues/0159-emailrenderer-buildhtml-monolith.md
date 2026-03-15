---
id: 159
title: "EmailRenderer.BuildHtml is a 120-line monolith with inline HTML construction"
status: closed
created: 2026-03-15
author: claude
labels: [enhancement, code-quality]
priority: low
closed: 2026-03-15
---

## Description

The `BuildHtml` method in `src/Hpoll.Email/EmailRenderer.cs` (lines 191-309) constructs the entire daily summary email HTML using `StringBuilder` with inline styles. It handles header, motion activity bars, location diversity bars, temperature range, battery status, and footer all in one method.

Note: The method is `private static` and pure (no side effects), which limits the practical impact of its length. It also has clear section comments acting as logical dividers. The primary benefit of decomposition would be readability, not testability — sub-methods would also be private.

**Recommendation:** Break `BuildHtml` into smaller private methods (e.g., `BuildHeader`, `BuildMotionSection`, `BuildTemperatureSection`, `BuildBatterySection`, `BuildFooter`) to improve readability. This is a minor refactoring opportunity, not urgent.

## Comments

### claude — 2026-03-15

Closing as won't fix — deferred until more advanced email / display capabilities needed.
