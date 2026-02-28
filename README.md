# hpoll

hpoll is an experimental Philips Hue monitoring service. It periodically polls
Hue Bridge hubs for motion and temperature sensor data, stores readings in a
SQLite database, and sends daily summary emails via AWS SES.

## Prerequisites

- A [Philips Hue developer account](https://developers.meethue.com/) with a
  registered Remote Hue API app (provides **Client ID** and **Client Secret**)
- A Hue Bridge on the same Hue account, with its **Bridge ID**
- A **Hue application key** (the `username` returned when you register a new
  user on the bridge via the link button) for each bridge
- An OAuth2 token set (access token, refresh token) obtained through the Hue
  Remote Authentication flow
- AWS credentials configured for SES (for daily email reports)

## Configuration

hpoll uses the standard .NET configuration system. Settings can be provided via
`appsettings.json`, environment variables, or command-line arguments. Environment
variables use `__` (double underscore) as section separators.

### Settings reference

| Setting | Env var | Default | Description |
|---|---|---|---|
| `DataPath` | `DataPath` | `data` | Directory for the SQLite database file |
| **Polling** | | | |
| `Polling:IntervalMinutes` | `Polling__IntervalMinutes` | `60` | Minutes between polling cycles |
| `Polling:BatteryPollIntervalHours` | `Polling__BatteryPollIntervalHours` | `84` | Hours between battery level polls (~twice per week) |
| `Polling:DataRetentionHours` | `Polling__DataRetentionHours` | `48` | Hours to keep device readings and polling logs before cleanup |
| `Polling:HttpTimeoutSeconds` | `Polling__HttpTimeoutSeconds` | `30` | HTTP client timeout for Hue API calls |
| `Polling:TokenRefreshCheckHours` | `Polling__TokenRefreshCheckHours` | `24` | Hours between token refresh checks |
| `Polling:TokenRefreshThresholdHours` | `Polling__TokenRefreshThresholdHours` | `48` | Hours before token expiry to trigger a refresh |
| `Polling:TokenRefreshMaxRetries` | `Polling__TokenRefreshMaxRetries` | `3` | Maximum retry attempts for token refresh |
| `Polling:HealthFailureThreshold` | `Polling__HealthFailureThreshold` | `3` | Consecutive poll failures before a hub is flagged unhealthy |
| `Polling:HealthMaxSilenceHours` | `Polling__HealthMaxSilenceHours` | `6` | Hours since last successful poll before a hub needs attention |
| **Email** | | | |
| `Email:SendTimesUtc` | `Email__SendTimesUtc__0`, `__1`, … | `["08:00"]` | List of times (UTC, `HH:mm`) to send summary emails |
| `Email:FromAddress` | `Email__FromAddress` | _(required)_ | Sender address for daily emails (must be SES-verified) |
| `Email:BatteryAlertThreshold` | `Email__BatteryAlertThreshold` | `30` | Battery % below which devices appear in the email alert section |
| `Email:BatteryLevelCritical` | `Email__BatteryLevelCritical` | `30` | Battery % below which the bar is red |
| `Email:BatteryLevelWarning` | `Email__BatteryLevelWarning` | `50` | Battery % below which the bar is yellow (green above) |
| `Email:SummaryWindowHours` | `Email__SummaryWindowHours` | `4` | Hours per time window in the daily summary email |
| `Email:SummaryWindowCount` | `Email__SummaryWindowCount` | `7` | Number of time windows in the daily summary email |
| `Email:ErrorRetryDelayMinutes` | `Email__ErrorRetryDelayMinutes` | `5` | Minutes to wait before retrying after an email scheduler error |
| `Email:AwsRegion` | `Email__AwsRegion` | `us-east-1` | AWS region for SES |
| **Hue app** | | | |
| `HueApp:ClientId` | `HueApp__ClientId` | _(required)_ | Hue Remote API app client ID |
| `HueApp:ClientSecret` | `HueApp__ClientSecret` | _(required)_ | Hue Remote API app client secret |
| `Customers` | _(see below)_ | `[]` | Array of customer/hub definitions |

### Customer and hub configuration

Customers and their linked hubs are defined as a JSON array. Each customer has a
name, email address (for daily reports), a timezone (for email bucketing), and
one or more hubs.

The `bridgeId` is the unique hardware identifier of the Hue Bridge (the serial
number printed on the device, also visible in the Hue app). It is not sent to
the Hue API — hpoll uses it internally as a unique key to match configuration
entries to database records and to identify hubs in log messages.

The `hueApplicationKey` is the `username` you receive when pressing the bridge
link button and creating a new API user (sometimes shown as a 40+ character hex
string). In the v1 CLIP API this was passed as a URL path segment; in the v2
Remote API hpoll sends it as the `hue-application-key` HTTP header alongside the
OAuth Bearer token.

```json
{
  "Customers": [
    {
      "name": "Jane Doe",
      "email": "jane@example.com",
      "timeZoneId": "Australia/Sydney",
      "hubs": [
        {
          "bridgeId": "001788FFFE123ABC",
          "hueApplicationKey": "your-hue-application-key",
          "accessToken": "initial-access-token",
          "refreshToken": "initial-refresh-token",
          "tokenExpiresAt": "2026-04-01T00:00:00Z"
        }
      ]
    }
  ]
}
```

The `timeZoneId` controls how readings are bucketed in the daily email. It
accepts any IANA timezone identifier (e.g. `Australia/Sydney`, `America/New_York`,
`Europe/London`). Defaults to `Australia/Sydney` if omitted. All data is stored
in UTC — the timezone is only used for email presentation.

When using environment variables, array elements are indexed with `__0__`,
`__1__`, etc.:

```
Customers__0__Name=Jane Doe
Customers__0__Email=jane@example.com
Customers__0__TimeZoneId=Australia/Sydney
Customers__0__Hubs__0__BridgeId=001788FFFE123ABC
Customers__0__Hubs__0__HueApplicationKey=your-hue-application-key
Customers__0__Hubs__0__AccessToken=initial-access-token
Customers__0__Hubs__0__RefreshToken=initial-refresh-token
Customers__0__Hubs__0__TokenExpiresAt=2026-04-01T00:00:00Z
```

On startup the service seeds customers and hubs from configuration into the
database. Token refresh is handled automatically after that.

## Running with Docker

The repository includes a `docker-compose.yml` and `.env.example` for quick
setup. The volume is configured as a **bind mount** (`./data:/app/data`) so the
SQLite database file is directly visible on the host at `./data/hpoll.db`.

> **Note:** Using a Docker named volume (e.g. `-v hpoll-data:/app/data`) stores
> the database inside Docker's internal storage, making it invisible on the host
> filesystem. The examples below use bind mounts instead.

### Data directory permissions

The containers run as a non-root user (`appuser`, UID 1000) for security. The
bind-mounted `./data` directory must be writable by this UID. Create it with the
correct ownership **before** starting the containers:

```bash
mkdir -p data
chown 1000:1000 data
```

If you already have a `./data` directory owned by root (e.g. from a previous
version that ran as root), fix the permissions:

```bash
sudo chown -R 1000:1000 data
```

Alternatively, if your host user's UID is not 1000, you can set the container
user to match your host UID in `docker-compose.yml`:

```yaml
services:
  worker:
    user: "${UID}:${GID}"
    # ...
  admin:
    user: "${UID}:${GID}"
    # ...
```

Then run with `UID=$(id -u) GID=$(id -g) docker compose up --build`.

### Docker Compose (recommended)

```bash
cp .env.example .env
# Edit .env with your credentials and settings
docker compose up --build
```

The included `docker-compose.yml` builds the image from source and reads
configuration from `.env`. To add customer/hub config, either set environment
variables in `.env` or mount an `appsettings.Production.json` file.

To add customer configuration via environment variables, append to `.env`:

```
Customers__0__Name=Jane Doe
Customers__0__Email=jane@example.com
Customers__0__TimeZoneId=Australia/Sydney
Customers__0__Hubs__0__BridgeId=001788FFFE123ABC
Customers__0__Hubs__0__HueApplicationKey=your-hue-application-key
Customers__0__Hubs__0__AccessToken=initial-access-token
Customers__0__Hubs__0__RefreshToken=initial-refresh-token
Customers__0__Hubs__0__TokenExpiresAt=2026-04-01T00:00:00Z
```

For multiple send times via environment variables:

```
Email__SendTimesUtc__0=06:00
Email__SendTimesUtc__1=18:00
```

Alternatively, mount an `appsettings.Production.json` file by adding this to
`docker-compose.yml` under the `worker` service volumes:

```yaml
- ./appsettings.Production.json:/app/appsettings.Production.json:ro
```

Where `appsettings.Production.json` contains:

```json
{
  "HueApp": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  },
  "Email": {
    "FromAddress": "alerts@example.com",
    "AwsRegion": "us-east-1",
    "SendTimesUtc": ["06:00", "18:00"],
    "BatteryAlertThreshold": 30,
    "BatteryLevelCritical": 30,
    "BatteryLevelWarning": 50,
    "SummaryWindowHours": 4,
    "SummaryWindowCount": 7,
    "ErrorRetryDelayMinutes": 5
  },
  "Polling": {
    "IntervalMinutes": 60,
    "BatteryPollIntervalHours": 84,
    "DataRetentionHours": 48,
    "HttpTimeoutSeconds": 30,
    "TokenRefreshCheckHours": 24,
    "TokenRefreshThresholdHours": 48,
    "TokenRefreshMaxRetries": 3,
    "HealthFailureThreshold": 3,
    "HealthMaxSilenceHours": 6
  },
  "Customers": [
    {
      "name": "Jane Doe",
      "email": "jane@example.com",
      "timeZoneId": "Australia/Sydney",
      "hubs": [
        {
          "bridgeId": "001788FFFE123ABC",
          "hueApplicationKey": "your-hue-application-key",
          "accessToken": "initial-access-token",
          "refreshToken": "initial-refresh-token",
          "tokenExpiresAt": "2026-04-01T00:00:00Z"
        }
      ]
    }
  ]
}
```

### Full `docker-compose.yml` example

A self-contained `docker-compose.yml` that pulls the pre-built image and
configures everything inline — no `.env` or JSON settings file needed:

```yaml
services:
  hpoll:
    image: pkinerd/hpoll:latest
    container_name: hpoll
    volumes:
      - ./data:/app/data
    restart: unless-stopped
    environment:
      # ── Hue app credentials ──────────────────────────────
      HueApp__ClientId: "your-client-id"
      HueApp__ClientSecret: "your-client-secret"

      # ── Polling ──────────────────────────────────────────
      Polling__IntervalMinutes: "60"
      Polling__BatteryPollIntervalHours: "84"
      Polling__DataRetentionHours: "48"
      Polling__HttpTimeoutSeconds: "30"
      Polling__TokenRefreshCheckHours: "24"
      Polling__TokenRefreshThresholdHours: "48"
      Polling__TokenRefreshMaxRetries: "3"
      Polling__HealthFailureThreshold: "3"
      Polling__HealthMaxSilenceHours: "6"

      # ── Email ────────────────────────────────────────────
      Email__FromAddress: "alerts@example.com"
      Email__AwsRegion: "ap-southeast-2"
      Email__SendTimesUtc__0: "06:00"
      Email__SendTimesUtc__1: "18:00"
      Email__BatteryAlertThreshold: "30"
      Email__BatteryLevelCritical: "30"
      Email__BatteryLevelWarning: "50"
      Email__SummaryWindowHours: "4"
      Email__SummaryWindowCount: "7"
      Email__ErrorRetryDelayMinutes: "5"

      # ── AWS credentials (SES) ───────────────────────────
      AWS_ACCESS_KEY_ID: ""
      AWS_SECRET_ACCESS_KEY: ""

      # ── Customer 1 ──────────────────────────────────────
      Customers__0__Name: "Jane Doe"
      Customers__0__Email: "jane@example.com"
      Customers__0__TimeZoneId: "Australia/Sydney"
      Customers__0__Hubs__0__BridgeId: "001788FFFE123ABC"
      Customers__0__Hubs__0__HueApplicationKey: "your-hue-application-key"
      Customers__0__Hubs__0__AccessToken: "initial-access-token"
      Customers__0__Hubs__0__RefreshToken: "initial-refresh-token"
      Customers__0__Hubs__0__TokenExpiresAt: "2026-04-01T00:00:00Z"

      # ── Customer 2 (optional) ───────────────────────────
      # Customers__1__Name: "John Smith"
      # Customers__1__Email: "john@example.com"
      # Customers__1__TimeZoneId: "America/New_York"
      # Customers__1__Hubs__0__BridgeId: "001788FFFE456DEF"
      # Customers__1__Hubs__0__HueApplicationKey: "..."
      # Customers__1__Hubs__0__AccessToken: "..."
      # Customers__1__Hubs__0__RefreshToken: "..."
      # Customers__1__Hubs__0__TokenExpiresAt: "..."
```

### `docker run`

```bash
mkdir -p data
docker run -d \
  --name hpoll \
  -v $(pwd)/data:/app/data \
  -e HueApp__ClientId=your-client-id \
  -e HueApp__ClientSecret=your-client-secret \
  -e Email__FromAddress=alerts@example.com \
  -e Email__AwsRegion=ap-southeast-2 \
  -e Email__SendTimesUtc__0=06:00 \
  -e Email__SendTimesUtc__1=18:00 \
  -e Customers__0__Name=Jane\ Doe \
  -e Customers__0__Email=jane@example.com \
  -e Customers__0__TimeZoneId=Australia/Sydney \
  -e Customers__0__Hubs__0__BridgeId=001788FFFE123ABC \
  -e Customers__0__Hubs__0__HueApplicationKey=your-hue-application-key \
  -e Customers__0__Hubs__0__AccessToken=initial-access-token \
  -e Customers__0__Hubs__0__RefreshToken=initial-refresh-token \
  -e Customers__0__Hubs__0__TokenExpiresAt=2026-04-01T00:00:00Z \
  pkinerd/hpoll:latest
```

## Building from source

```bash
dotnet restore
dotnet build -c Release
dotnet run --project src/Hpoll.Worker
```

### Docker

```bash
docker build -t hpoll .
```

## Data persistence

The SQLite database is stored at `<DataPath>/hpoll.db` (`/app/data/hpoll.db`
inside the container). Use a bind mount (`-v ./data:/app/data`) to persist data
across container restarts and make the database file accessible on the host at
`./data/hpoll.db`.
