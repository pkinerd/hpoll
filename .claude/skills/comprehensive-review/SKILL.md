---
name: comprehensive-review
description: Perform a comprehensive set of code reviews (quality, security, testing, coverage, documentation) using parallel sub-agents, collate all findings, and create or update issues for each actionable finding. Use when you want a full-spectrum review of the codebase.
user_invocable: true
---

# Comprehensive Review

## Instructions

Perform a comprehensive, multi-faceted review of the codebase by running several
independent review workstreams in parallel using sub-agents, then collating the
results and creating or updating issues for each actionable finding.

**Important:** Each review is complex and produces significant context. You MUST
delegate each review to a separate sub-agent using the Agent tool to avoid
exceeding context limits. Do NOT attempt to inline the reviews in the main
conversation.

### Step 1: Preparation

Before launching reviews, complete these preparation steps sequentially:

#### 1a. Install required tools

Ensure the .NET 8.0 SDK is available (required for build, test, and coverage
analysis):

```bash
dotnet --version 2>/dev/null || (sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0)
```

#### 1b. Extract Hue API documentation

Invoke the `/hue-api-docs` skill to extract the Hue API documentation into a
temporary directory. Record the extraction path — sub-agents that need API
context (security review, documentation review) will need this path.

If the `HUE_API_DOCS_PASSWORD` environment variable is not set, note this and
continue — the reviews can still proceed without it, but API-specific findings
may be less detailed.

#### 1c. Obtain latest build and test logs

Invoke the `/poll-build-logs` skill to check for the latest CI build results.
If a matching build is found, record the build status and any test failures.
If no build is found (e.g., no recent push), note this and continue.

These logs provide valuable context for the testing and coverage reviews.

### Step 2: Launch parallel reviews

Launch ALL FIVE review sub-agents in parallel using the Agent tool. Each agent
should be given the full context it needs to operate independently. Wait for all
agents to complete before proceeding to Step 3.

**Critical:** Use a single message with multiple Agent tool calls to launch all
five reviews concurrently. Each agent must return a structured list of findings.

#### Review 1: Code quality review (simplify skill)

Launch a `general-purpose` sub-agent with this task:

> Perform a code quality review of the hpoll codebase. Use the `/simplify`
> skill to review changed code for reuse, quality, and efficiency. If there are
> no recent changes on the current branch compared to the base branch, review
> the overall codebase quality by examining key source files across all projects
> (src/Hpoll.Core, src/Hpoll.Data, src/Hpoll.Worker, src/Hpoll.Email,
> src/Hpoll.Admin).
>
> Focus on:
> - Code duplication and opportunities for reuse
> - Design patterns and architectural quality
> - Error handling consistency
> - Naming conventions and code clarity
> - Performance concerns (inefficient queries, unnecessary allocations, etc.)
> - Adherence to the project's coding conventions (see CLAUDE.md)
>
> Return your findings as a structured list. For each finding, provide:
> 1. **Category**: quality, duplication, performance, naming, architecture
> 2. **Severity**: low, medium, high, critical
> 3. **Location**: file path and line number(s)
> 4. **Description**: what the issue is
> 5. **Recommendation**: how to fix it
>
> Do NOT make any code changes — this is a review only.

#### Review 2: Security review

Launch a `general-purpose` sub-agent with this task:

> Perform a security review of the hpoll codebase. Examine all source code in
> the src/ directory for security vulnerabilities and weaknesses.
>
> Focus on:
> - OWASP Top 10 vulnerabilities (injection, XSS, broken auth, etc.)
> - Secrets or credentials in source code or configuration
> - Input validation and sanitization (especially in the admin portal Razor Pages)
> - Authentication and authorization flaws
> - SQL injection risks (even with EF Core, check for raw SQL usage)
> - Insecure HTTP client configuration (certificate validation, TLS)
> - Sensitive data exposure in logs or error messages
> - CSRF protection in the admin portal
> - Dependency vulnerabilities (check for known vulnerable packages)
> - Docker security (running as root, exposed ports, secrets in images)
> - API key/token handling (Hue Bridge tokens, AWS SES credentials)
>
> If Hue API docs are available at: [include extraction path if available],
> also check that API calls follow security best practices from the docs.
>
> Return your findings as a structured list. For each finding, provide:
> 1. **Category**: injection, auth, config, secrets, crypto, dependency, docker
> 2. **Severity**: low, medium, high, critical
> 3. **Location**: file path and line number(s)
> 4. **Description**: what the vulnerability is
> 5. **Recommendation**: how to fix it
> 6. **OWASP reference**: if applicable (e.g., A01:2021-Broken Access Control)
>
> Do NOT make any code changes — this is a review only.

#### Review 3: Unit testing review

Launch a `general-purpose` sub-agent with this task:

> Perform a unit testing review of the hpoll codebase. Examine all test
> projects under tests/ and the source code they cover.
>
> Run the test suite first to understand the current state:
> ```bash
> dotnet test --no-build -c Release 2>/dev/null || dotnet test
> ```
>
> Then review:
> - **Test coverage gaps**: identify source classes/methods that lack unit tests
> - **Test quality**: are tests testing meaningful behavior or just implementation?
> - **Test organization**: do tests follow consistent patterns and naming?
> - **Missing edge cases**: are error paths, boundary conditions, and null cases tested?
> - **Mock usage**: is Moq used appropriately? Are there over-mocked tests?
> - **Test isolation**: do tests have shared state or ordering dependencies?
> - **Assertion quality**: are assertions specific enough? Are there tests with no assertions?
> - **Missing test projects**: are there source projects without corresponding test projects?
>   (e.g., Hpoll.Email, Hpoll.Data, Hpoll.Admin may lack test coverage)
>
> Map out which source files/classes have tests and which do not. Pay special
> attention to critical business logic in Hpoll.Core (HueApiClient,
> HealthEvaluator) and Hpoll.Worker (background services).
>
> Return your findings as a structured list. For each finding, provide:
> 1. **Category**: coverage-gap, test-quality, missing-edge-case, organization, isolation
> 2. **Severity**: low, medium, high, critical
> 3. **Location**: source file lacking coverage or test file with the issue
> 4. **Description**: what is missing or wrong
> 5. **Recommendation**: specific test(s) that should be added or changes to make
>
> Do NOT write any tests — this is a review only.

#### Review 4: Code coverage analysis

Launch a `general-purpose` sub-agent with this task:

> Perform a code coverage analysis of the hpoll codebase. Run the test suite
> with coverage collection and analyze the results.
>
> Steps:
> 1. Build and run tests with coverage:
>    ```bash
>    dotnet restore
>    dotnet build --no-restore -c Release
>    dotnet test --no-build -c Release \
>      --settings coverlet.runsettings \
>      --collect:"XPlat Code Coverage" \
>      --results-directory TestResults
>    ```
>
> 2. If a coverage report tool is available (reportgenerator), generate a
>    summary. Otherwise, parse the Cobertura XML files in TestResults/:
>    ```bash
>    find TestResults -name "coverage.cobertura.xml" -exec cat {} \;
>    ```
>
> 3. Analyze the coverage data to determine:
>    - Overall line coverage percentage
>    - Overall branch coverage percentage
>    - Per-project coverage breakdown
>    - Files/classes with lowest coverage
>    - Uncovered critical paths (business logic, error handling)
>    - Projects with zero coverage (no test project at all)
>
> Return your findings as a structured list. For each finding, provide:
> 1. **Category**: uncovered-file, low-coverage, uncovered-branch, no-test-project
> 2. **Severity**: low (>80%), medium (50-80%), high (20-50%), critical (<20%)
> 3. **Location**: file path or project name
> 4. **Description**: current coverage level and what is missing
> 5. **Recommendation**: which lines/branches to prioritize for testing
>
> Include an overall coverage summary at the top of your findings.
>
> Do NOT write any tests — this is an analysis only.

#### Review 5: Code documentation review

Launch a `general-purpose` sub-agent with this task:

> Perform a code documentation review of the hpoll codebase. Examine source
> code across all projects for documentation quality.
>
> Review:
> - **XML doc comments**: are public APIs documented with `<summary>`, `<param>`,
>   `<returns>`, and `<exception>` tags?
> - **Interface documentation**: are interfaces (in Hpoll.Core/Interfaces/)
>   well-documented so implementations are clear?
> - **Complex logic**: are non-obvious algorithms or business rules explained?
> - **Configuration**: are configuration options (appsettings, Options classes)
>   documented with expected values and defaults?
> - **README/CLAUDE.md accuracy**: does the project documentation match the
>   current state of the code?
> - **Migration comments**: do EF Core migrations have meaningful names and
>   descriptions?
> - **Inline comments**: are there misleading, outdated, or unnecessary comments?
> - **Missing documentation**: are there complex public methods with no
>   documentation at all?
>
> If Hue API docs are available at: [include extraction path if available],
> also check that code comments accurately describe Hue API behavior.
>
> Return your findings as a structured list. For each finding, provide:
> 1. **Category**: missing-docs, outdated-docs, misleading-docs, config-docs, readme
> 2. **Severity**: low, medium, high
> 3. **Location**: file path and line number(s) or section
> 4. **Description**: what is missing or incorrect
> 5. **Recommendation**: what documentation should be added or fixed
>
> Do NOT add any documentation — this is a review only.

### Step 3: Collate findings

After all five sub-agents complete, gather their results into a unified review
report. Organize the report as follows:

1. **Executive Summary**: brief overview of the codebase health across all
   review dimensions, with a severity breakdown (critical/high/medium/low counts)

2. **Critical and High Findings**: list all critical and high severity findings
   from across all reviews, grouped by review type

3. **Medium Findings**: list all medium severity findings, grouped by review type

4. **Low Findings**: briefly summarize low severity findings (do not need
   individual issue creation unless they represent a pattern)

5. **Coverage Summary**: include the overall coverage statistics from Review 4

Present this collated report to the user before proceeding to issue creation.

### Step 4: Review existing issues

Invoke the `/issues` skill with `list` to retrieve all current issues. Then
review the existing issues to understand what has already been tracked.

For each finding from Step 3, determine whether:
- **It is a new issue**: no existing issue covers this finding
- **It adds nuance to an existing issue**: an existing issue covers the same
  area but the finding adds new detail, a different angle, or updated severity
- **It is already fully tracked**: an existing issue already captures this
  finding — skip it

### Step 5: Create or update issues

For each finding that warrants action (from the assessment in Step 4):

#### New issues

Use the `/issues` skill with `create` to create a new issue. Include:
- **Title**: concise description of the finding
- **Description**: full detail from the review finding, including location,
  severity, category, and recommendation
- **Labels**: use appropriate labels — map review categories to issue labels:
  - Code quality findings → `enhancement`
  - Security findings → `bug` (for vulnerabilities) or `enhancement` (for hardening)
  - Testing gaps → `enhancement`
  - Coverage gaps → `enhancement`
  - Documentation gaps → `documentation`
- **Priority**: map directly from the finding severity (low/medium/high/critical)

#### Existing issues to update

Use the `/issues` skill with `comment` to add a comment to the existing issue
with the new findings or nuance from this review. Reference which review
produced the new information.

**Batch operations**: If there are many new issues to create (more than 5), use
the bulk issue creation workflow described in the issues skill to create them
all in a single commit rather than one at a time.

### Step 6: Summary

Present a final summary to the user:

1. Total findings by severity across all reviews
2. Number of new issues created (with IDs and titles)
3. Number of existing issues updated (with IDs)
4. Number of findings already tracked (skipped)
5. Top recommendations for immediate action (critical/high findings)

## Example Flow

```
User: /comprehensive-review

Claude:
1. Checks dotnet SDK is installed — yes
2. Invokes /hue-api-docs — extracts to /tmp/hue_api_docs_xyz/
3. Invokes /poll-build-logs — finds passing build for current commit
4. Launches 5 sub-agent reviews in parallel:
   - Code quality review agent
   - Security review agent
   - Unit testing review agent
   - Code coverage analysis agent
   - Code documentation review agent
5. [All agents complete]
6. Collates findings:
   - 2 critical, 5 high, 12 medium, 8 low findings
7. Reviews existing issues — finds 3 open issues
8. Creates 15 new issues, adds comments to 2 existing issues
9. Presents summary:
   "Comprehensive review complete:
    - 27 findings across 5 review areas
    - 15 new issues created (#4-#18)
    - 2 existing issues updated (#1, #3)
    - 2 findings already tracked (skipped)
    Top priorities: [critical findings listed]"
```

## Important Notes

- **Sub-agents are mandatory**: each review MUST run in its own sub-agent to
  manage context effectively. Never attempt to run all reviews inline.
- **Parallel execution**: launch all 5 review agents in a single message to
  maximize throughput. They are fully independent of each other.
- **Read-only reviews**: no sub-agent should modify code, write tests, or add
  documentation. All reviews are analysis-only.
- **Issue deduplication**: always check existing issues before creating new ones
  to avoid duplicates.
- **Hue API docs**: if extraction fails, proceed without them. Note in the
  security and documentation reviews that API-specific checks were limited.
- **Build logs**: if no recent build is found, note this in the report but
  proceed with the reviews — the code can still be analyzed statically.
