---
id: 106
title: "HueApiClient deserializes JSON via intermediate string allocation"
status: open
created: 2026-03-02
author: claude
labels: [enhancement, performance]
priority: low
---

## Description

In `HueApiClient.GetResourceAsync` (lines 163-182), the code reads the full HTTP response body as a string with `ReadAsStringAsync()` and then deserializes it with `JsonSerializer.Deserialize<T>()`. This creates a full in-memory copy of the response JSON as a string before the deserialized objects.

For large device/sensor lists, the JSON exists twice in memory: once as a string, once as deserialized objects. The same pattern applies to `PostTokenRequestAsync` (line 208).

**Location:** `src/Hpoll.Core/Services/HueApiClient.cs`, lines 178, 208

**Recommendation:**
Use `response.Content.ReadAsStreamAsync()` followed by `JsonSerializer.DeserializeAsync<T>(stream)` to avoid the intermediate string allocation.

**However**, the practical impact is negligible for this use case. Hue Bridge responses are small (10-50 devices, ~15-20 KB of JSON). The polling frequency is low (every few minutes). The intermediate string allocation is collected almost instantly. Additionally, having the raw `json` string available aids debugging — if deserialization fails, the raw response can be inspected or logged.

**Verdict:** This is a code style preference rather than a meaningful performance concern. Consider closing as won't-fix.

## Comments
