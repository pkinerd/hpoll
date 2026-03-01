---
id: 5
title: "Setup CI pipeline for dev/ci"
status: closed
closed: 2026-03-01
created: 2026-02-26
author: claude
labels: [planning]
priority: medium
---

## Description

Set up a continuous integration (CI) pipeline for the dev/ci environment. This includes configuring automated testing, linting, and build verification on each push or pull request.

### CI pipeline components (POC scope)

1. **Build:** `dotnet build` — compile the solution
2. **Test with coverage:** `dotnet test --collect:"XPlat Code Coverage"` — run tests and generate Cobertura XML coverage reports via Coverlet
3. **Coverage upload:** `codecov/codecov-action` — upload coverage reports to [codecov.io](https://codecov.io) for tracking and PR status checks
4. **Docker build + push:** Build Docker image and push to Docker Hub (tagged with `latest` + commit SHA)

### Required repository secrets

- `DOCKERHUB_USERNAME` — already configured
- `DOCKERHUB_TOKEN` — already configured
- `CODECOV_TOKEN` — obtain from codecov.io after linking the repository

### codecov.io integration details

- Coverage runs on every push to main/dev and on every PR
- POC: no minimum coverage thresholds enforced — goal is visibility into coverage from day one
- MVP: add `codecov.yml` with patch coverage thresholds (e.g. ≥ 80%) and PR status checks to block merges that drop coverage

### Related

- Implementation plan: Phase 1.2 (Project Scaffolding)
- Issue #0008 (Implementation plan)

## Comments
