---
id: 60
title: "Consolidate Docker CI tag-building script into reusable action or shared script"
status: open
created: 2026-03-01
author: claude
labels: [enhancement]
priority: low
---

## Description

The "Extract metadata" step in the CI workflow is copy-pasted between the `docker` and `docker-admin` jobs (lines 100-119 vs 151-170). The only difference is the image name (`hpoll` vs `hpoll-admin`). The entire 20-line bash script for computing `SHORT_SHA`, building the tag list based on branch/PR context, and sanitizing branch names is identical.

**File:** `.github/workflows/build-and-test.yml:100-119, 151-170`

**Recommended fix:** Either:
1. Extract into a reusable composite action (`.github/actions/docker-metadata/action.yml`) that accepts the image name as input
2. Use a matrix strategy with a single Docker job parameterized by image name and Dockerfile path
3. Extract to a shared shell script (`.github/scripts/compute-tags.sh`) called by both jobs

Option 2 (matrix) would be the cleanest, reducing two near-identical jobs to one.

**Source:** Code reuse review finding R9

## Comments

### claude — 2026-03-01

Critical review: VALID with caveats. The duplication is real but the issue's framing warrants clarification and a more nuanced recommendation.

**Accuracy of the issue description:**

- The line numbers are slightly off. The "Extract metadata" scripts are at lines 106-125 and 163-182, not 100-119 and 151-170.
- The issue correctly identifies that only the image name differs in the tag-building script. However, it understates the scope: the entire `docker` (lines 85-140) and `docker-admin` (lines 142-198) jobs are near-identical -- 56 lines each with only 5 lines of difference: (1) job/step name, (2) image name suffix (`hpoll` vs `hpoll-admin`), (3) the `file:` parameter (absent vs `Dockerfile.admin`), and (4) the cache scope (`type=gha` vs `type=gha,scope=admin`).

**Assessment of the recommended fixes:**

Option 2 (matrix strategy) is the strongest approach and would genuinely reduce ~56 lines of duplication down to a single parameterized job. A matrix like:

```yaml
strategy:
  matrix:
    include:
      - image_suffix: hpoll
        dockerfile: Dockerfile
        cache_scope: worker
      - image_suffix: hpoll-admin
        dockerfile: Dockerfile.admin
        cache_scope: admin
```

would cleanly eliminate the duplication. However, there are two complications the issue does not mention:

1. **`push-build-logs` coupling.** The `push-build-logs` job (line 209) has `needs: [build-and-test, docker, docker-admin]` and its status-checking script (lines 219-224) references both `needs.docker.result` and `needs.docker-admin.result` by name. A matrix refactor would collapse these into a single job name, simplifying the `needs` list but requiring the status check to be updated. This is minor but worth noting.

2. **Parallel execution.** The two separate jobs currently run in parallel on different runners. A matrix strategy preserves this parallelism, so there is no performance regression -- this is a non-issue but worth confirming.

Option 1 (composite action) adds a layer of indirection (a new directory, a new YAML file) for a 20-line script that is only used twice. This is over-engineering for the current scale. Option 3 (shared shell script) is reasonable but less idiomatic for GitHub Actions than a matrix.

**Is this worth doing?**

With only 2 Docker targets and a total of ~56 duplicated lines, this is a legitimate but low-impact improvement. The duplication is unlikely to cause a real maintenance bug because changes to the tag logic would naturally be applied to both jobs during review. That said, if a third Docker target were added (which is plausible if the project grows additional services), the matrix approach would pay for itself immediately. The issue is correctly marked as low priority.

**Verdict:** Valid, low priority. The matrix strategy (option 2) is the right recommendation. The issue should note the `push-build-logs` coupling as a secondary consideration. Not urgent -- the current duplication is manageable and unlikely to cause drift with only 2 targets.
