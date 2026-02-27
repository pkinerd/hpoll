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
| `Polling:IntervalMinutes` | `Polling__IntervalMinutes` | `60` | Minutes between polling cycles |
| `Email:SendTimesUtc` | `Email__SendTimesUtc__0`, `__1`, … | `["08:00"]` | List of times (UTC, `HH:mm`) to send summary emails |
| `Email:FromAddress` | `Email__FromAddress` | _(required)_ | Sender address for daily emails (must be SES-verified) |
| `Email:AwsRegion` | `Email__AwsRegion` | `us-east-1` | AWS region for SES |
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
    "SendTimesUtc": ["06:00", "18:00"]
  },
  "Polling": {
    "IntervalMinutes": 60
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

Below is a complete `docker-compose.yml` that inlines all configuration. This
is useful when you don't want a separate `.env` or JSON settings file:

```yaml
services:
  worker:
    build: .
    volumes:
      - ./data:/app/data
    restart: unless-stopped
    environment:
      # Hue app credentials
      HueApp__ClientId: "your-client-id"
      HueApp__ClientSecret: "your-client-secret"

      # Email settings — multiple send times supported
      Email__FromAddress: "alerts@example.com"
      Email__AwsRegion: "us-east-1"
      Email__SendTimesUtc__0: "06:00"
      Email__SendTimesUtc__1: "18:00"

      # AWS credentials for SES
      AWS_ACCESS_KEY_ID: ""
      AWS_SECRET_ACCESS_KEY: ""

      # Polling interval
      Polling__IntervalMinutes: "60"

      # Customer 1
      Customers__0__Name: "Jane Doe"
      Customers__0__Email: "jane@example.com"
      Customers__0__TimeZoneId: "Australia/Sydney"
      Customers__0__Hubs__0__BridgeId: "001788FFFE123ABC"
      Customers__0__Hubs__0__HueApplicationKey: "your-hue-application-key"
      Customers__0__Hubs__0__AccessToken: "initial-access-token"
      Customers__0__Hubs__0__RefreshToken: "initial-refresh-token"
      Customers__0__Hubs__0__TokenExpiresAt: "2026-04-01T00:00:00Z"
```

### `docker run`

```bash
mkdir -p data
docker run -d \
  --name hpoll \
  -v $(pwd)/data:/app/data \
  --env-file .env \
  -e Customers__0__Name=Jane\ Doe \
  -e Customers__0__Email=jane@example.com \
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
