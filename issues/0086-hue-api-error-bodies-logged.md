---
id: 86
title: "Hue API error response bodies logged with potential sensitive data"
status: open
created: 2026-03-01
author: claude
labels: [security]
priority: low
---

## Description

When Hue API calls fail, `HueApiClient` logs the error response body (truncated to 500 chars)
at Warning level in multiple locations (lines 96-97, 120-121, 169-170, 200-201). Additionally,
line 141 logs the full JSON response on unexpected registration format.

These error responses could contain:
- Token values or internal API details from the Hue service
- Information useful for reconnaissance if logs are exposed

This is distinct from #64 (sanitize exception messages in PollingLog) — this finding is about
direct Warning-level log output in the HueApiClient service.

**Found by:** Comprehensive review — security review.

**OWASP reference:** A09:2021-Security Logging and Monitoring Failures

**Recommendation:** Log only HTTP status codes and generic messages at Warning level. Gate
response body logging behind `Debug` log level. Redact potential token or credential values.

## Comments

### claude — 2026-03-01

**Critical review: PARTIALLY_VALID — multiple flaws in the reasoning. Priority remains low but description overstates the risk.**

#### Flaw 1: Line 141 is mischaracterized as "logging"

The issue states "line 141 logs the full JSON response on unexpected registration format." This is incorrect. Line 141 is:

```csharp
throw new InvalidOperationException($"Unexpected registration response format: {json}");
```

This is a `throw`, not a log statement. The JSON body is embedded in an exception message, which is a fundamentally different concern from Warning-level logging. Whether and where this exception message appears depends entirely on how the caller catches it. This was already identified and analyzed as a separate concern in issue #53 (RegisterApplicationAsync leaks response body in exception). The critical review on #64 also noted that #53's consolidation into #64 was incorrect because `RegisterApplicationAsync` is called from the OAuth flow, not the polling path.

Conflating a thrown exception with a log statement muddies the analysis.

#### Flaw 2: "Token values" claim is not substantiated by how the Hue API works

The issue claims error responses "could contain token values or internal API details." This needs scrutiny for each of the four logging locations:

1. **EnableLinkButtonAsync** (PUT `/api/0/config`) — Hue v1 API error responses use the format `[{"error":{"type":101,"address":"/config","description":"link button not pressed"}}]`. Error type codes and human-readable descriptions. No tokens.

2. **RegisterApplicationAsync** (POST `/api`) — Same Hue v1 error format. Error responses do NOT echo back the Bearer token that was sent as a header.

3. **GetResourceAsync** (GET CLIP v2 paths like `/resource/motion`) — CLIP v2 error responses use `{"errors":[{"description":"..."}]}`. Structured error descriptions. The access token and application key are sent as HTTP headers — standard HTTP APIs do not echo request headers back in error response bodies.

4. **PostTokenRequestAsync** (POST OAuth2 token endpoint) — OAuth2 error responses follow RFC 6749 Section 5.2: `{"error":"invalid_grant","error_description":"..."}`. The RFC explicitly does NOT echo back the client credentials, refresh token, or authorization code in error responses. The Hue OAuth2 implementation follows this standard.

The claim that "token values" could appear in error response bodies is unsupported. None of the four Hue API endpoints echo credentials or tokens in their error responses. The issue should not have listed this as a threat vector without evidence.

#### Flaw 3: Redundancy with existing analysis on #64

The issue claims distinctness from #64, but the critical review on #64 already analyzed HueApiClient's Warning-level logging in detail and explicitly called it "tangential" and a "structured logging hygiene" concern that "should not be conflated with the PollingLog persistence question." Issue #86 does not add any new analysis beyond what the #64 comments already covered — it simply restates the same finding as a standalone issue without addressing the counterarguments already raised.

#### Flaw 4: OWASP reference is loosely applied

A09:2021 (Security Logging and Monitoring Failures) is primarily about _insufficient_ logging — failure to detect attacks, missing audit trails, and inadequate alerting. While A09 does include a sub-concern about "sensitive data stored in logs," it only applies if the data being logged is actually sensitive. Since Hue API error responses contain error codes and descriptions (not tokens or credentials), the OWASP reference is a stretch. This finding is better characterized as general logging hygiene rather than a security vulnerability.

#### Flaw 5: Recommendation to "redact potential token or credential values" is misguided

Since Hue API error responses do not contain tokens or credentials, there is nothing to redact. This recommendation implies a threat model that doesn't match the actual API behavior. It would add unnecessary complexity (building a redaction mechanism) to address a non-existent problem.

#### What IS valid

- The four Warning-level log statements do log external API response bodies, which is not ideal logging hygiene. Gating response body content behind Debug level is a reasonable best practice.
- Error responses could contain internal Hue bridge diagnostic details (error types, internal paths like `/config`), which are mildly useful for reconnaissance if logs are compromised — but this is very low severity.
- The recommendation to "log only HTTP status codes and generic messages at Warning level" is proportionate and sensible.

#### Verdict

The issue correctly identifies that HueApiClient logs error response bodies at Warning level. However, it significantly overstates the sensitivity of the data being logged by claiming "token values" without evidence, mischaracterizes line 141 (a throw) as logging, repeats analysis already covered in #64's comments, and applies an OWASP category that doesn't fit well. The recommendation to add credential redaction is solving a non-existent problem.

**Recommended changes to this issue:**
- Remove the claim about "token values" — Hue API error responses do not contain them
- Remove the line 141 reference — it's a throw, not a log, and is already tracked in #53
- Acknowledge the overlap with #64's analysis
- Simplify the recommendation to just: move response body logging from Warning to Debug level
- Consider closing as a duplicate of the logging hygiene concern already documented in #64's comments
