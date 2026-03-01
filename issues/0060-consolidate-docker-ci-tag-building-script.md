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
