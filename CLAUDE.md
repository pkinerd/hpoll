# CLAUDE.md

## Project Overview

hpoll is a Philips Hue monitoring service that polls Hue Bridge hubs for motion and temperature sensor data, stores readings in SQLite, and sends daily summary emails via AWS SES. It includes a web admin portal for managing customers and hubs.

## Tech Stack

- **Language:** C# on .NET 8.0
- **Database:** SQLite with Entity Framework Core 8.0
- **Web:** ASP.NET Core Razor Pages (admin portal)
- **Email:** AWS SES
- **Testing:** xUnit 2.4.2, Moq
- **CI:** GitHub Actions
- **Containers:** Docker & Docker Compose

## Project Structure

```
src/
  Hpoll.Core/          # Interfaces, models, configuration, core services (HueApiClient, HealthEvaluator)
  Hpoll.Data/          # EF Core DbContext, entities, migrations, config seeder
  Hpoll.Worker/        # Background services (polling, token refresh, email scheduler)
  Hpoll.Email/         # Email rendering (HTML) and SES sending
  Hpoll.Admin/         # Razor Pages web admin portal
tests/
  Hpoll.Core.Tests/    # Unit tests for core logic
  Hpoll.Worker.Tests/  # Unit tests for worker services
```

## Environment Setup

The .NET 8.0 SDK must be installed before building or testing locally. If `dotnet` is not available, install it first:

```bash
# Install .NET 8.0 SDK (Ubuntu/Debian)
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

## Build & Test Commands

```bash
# Restore, build, and test (matches CI)
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release \
  --settings coverlet.runsettings \
  --collect:"XPlat Code Coverage" \
  --logger "junit;LogFilePath=TestResults/{assembly}.junit.xml" \
  --results-directory TestResults
```

Quick shortcuts:
```bash
dotnet build              # Debug build
dotnet test               # Run all tests (debug, no coverage)
dotnet run --project src/Hpoll.Worker    # Run worker locally
dotnet run --project src/Hpoll.Admin     # Run admin portal locally
```

## Code Conventions

- **Namespaces:** `Hpoll.<Project>` (e.g., `Hpoll.Core.Services`)
- **Private fields:** `_camelCase` prefix (e.g., `_logger`, `_httpClientFactory`)
- **Async methods:** suffix with `Async` (e.g., `GetMotionSensorsAsync`)
- **Configuration:** Options pattern with `IOptions<T>`, environment variables use `__` separator (e.g., `Polling__IntervalMinutes`)
- **Background services:** inherit `BackgroundService`, use `IServiceScopeFactory` for scoped dependencies

## Database

SQLite file at `<DataPath>/hpoll.db` (default DataPath: `data`). Migrations auto-apply on startup. EF Core migrations live in `src/Hpoll.Data/Migrations/`.

To add a migration:
```bash
dotnet ef migrations add <Name> --project src/Hpoll.Data --startup-project src/Hpoll.Worker
```

## Docker

```bash
docker build -t hpoll .                          # Worker
docker build -f Dockerfile.admin -t hpoll-admin . # Admin
docker compose up --build                         # Full stack
```

## CI

GitHub Actions workflow (`.github/workflows/build-and-test.yml`) runs on pushes to `main`, `dev`, and `claude/*` branches. It builds, runs tests with coverage, then builds and pushes Docker images.

## Skills (Slash Commands)

Always use the relevant skill below instead of attempting the task manually. For example, use `/prepare-pr` to create pull requests rather than running `gh pr create` directly, and use `/poll-build-logs` to check CI results rather than manually fetching logs.

- **`/issues`** — Manage issues tracked on the `claude/issues` orphan branch. Use this to list, create, show, update, comment on, close, reopen, search, and filter issues. Always use this skill instead of manually managing issue tracking.
- **`/hue-api-docs`** — Extract Philips Hue API documentation from the encrypted archive into a temporary directory. Use this whenever you need to reference Hue API endpoints, payloads, or behavior (e.g., when implementing or modifying API calls).
- **`/poll-build-logs`** — Poll for CI build log branches after pushing code. Use this after every push to monitor whether the build passed or failed, and to analyze test results and build output.
- **`/prepare-pr`** — Generate PR title, description, and URL for creating a pull request from the current branch. Accepts an optional target branch argument (e.g., `/prepare-pr dev`).
- **`/simplify`** — Review changed code for reuse, quality, and efficiency, then fix any issues found.
- **`/comprehensive-review`** — Run a full-spectrum codebase review (code quality, security, unit testing, coverage, documentation) using parallel sub-agents. Collates all findings and creates or updates issues for each actionable result.
