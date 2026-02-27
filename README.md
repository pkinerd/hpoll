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
| `Email:SendTimeUtc` | `Email__SendTimeUtc` | `08:00` | Time (UTC) to send daily summary emails |
| `Email:FromAddress` | `Email__FromAddress` | _(required)_ | Sender address for daily emails (must be SES-verified) |
| `Email:AwsRegion` | `Email__AwsRegion` | `us-east-1` | AWS region for SES |
| `HueApp:ClientId` | `HueApp__ClientId` | _(required)_ | Hue Remote API app client ID |
| `HueApp:ClientSecret` | `HueApp__ClientSecret` | _(required)_ | Hue Remote API app client secret |
| `Customers` | _(see below)_ | `[]` | Array of customer/hub definitions |

### Customer and hub configuration

Customers and their linked hubs are defined as a JSON array. Each customer has a
name, email address (for daily reports), and one or more hubs.

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

When using environment variables, array elements are indexed with `__0__`,
`__1__`, etc.:

```
Customers__0__Name=Jane Doe
Customers__0__Email=jane@example.com
Customers__0__Hubs__0__BridgeId=001788FFFE123ABC
Customers__0__Hubs__0__HueApplicationKey=your-hue-application-key
Customers__0__Hubs__0__AccessToken=initial-access-token
Customers__0__Hubs__0__RefreshToken=initial-refresh-token
Customers__0__Hubs__0__TokenExpiresAt=2026-04-01T00:00:00Z
```

On startup the service seeds customers and hubs from configuration into the
database. Token refresh is handled automatically after that.

## Running with Docker

### `docker run`

```bash
docker run -d \
  --name hpoll \
  -v hpoll-data:/app/data \
  -e HueApp__ClientId=your-client-id \
  -e HueApp__ClientSecret=your-client-secret \
  -e Email__FromAddress=alerts@example.com \
  -e Email__AwsRegion=us-east-1 \
  -e Polling__IntervalMinutes=60 \
  -e Email__SendTimeUtc=08:00 \
  -e Customers__0__Name=Jane\ Doe \
  -e Customers__0__Email=jane@example.com \
  -e Customers__0__Hubs__0__BridgeId=001788FFFE123ABC \
  -e Customers__0__Hubs__0__HueApplicationKey=your-hue-application-key \
  -e Customers__0__Hubs__0__AccessToken=initial-access-token \
  -e Customers__0__Hubs__0__RefreshToken=initial-refresh-token \
  -e Customers__0__Hubs__0__TokenExpiresAt=2026-04-01T00:00:00Z \
  -e AWS_ACCESS_KEY_ID=your-aws-key \
  -e AWS_SECRET_ACCESS_KEY=your-aws-secret \
  pkinerd/hpoll:latest
```

### Docker Compose

```yaml
services:
  hpoll:
    image: pkinerd/hpoll:latest
    container_name: hpoll
    restart: unless-stopped
    volumes:
      - hpoll-data:/app/data
    environment:
      # Hue Remote API app credentials
      HueApp__ClientId: "your-client-id"
      HueApp__ClientSecret: "your-client-secret"

      # Polling
      Polling__IntervalMinutes: "60"

      # Daily email settings
      Email__FromAddress: "alerts@example.com"
      Email__AwsRegion: "us-east-1"
      Email__SendTimeUtc: "08:00"

      # AWS credentials for SES
      AWS_ACCESS_KEY_ID: "your-aws-key"
      AWS_SECRET_ACCESS_KEY: "your-aws-secret"

      # Customer: Jane Doe with one linked hub
      Customers__0__Name: "Jane Doe"
      Customers__0__Email: "jane@example.com"
      Customers__0__Hubs__0__BridgeId: "001788FFFE123ABC"
      Customers__0__Hubs__0__HueApplicationKey: "your-hue-application-key"
      Customers__0__Hubs__0__AccessToken: "initial-access-token"
      Customers__0__Hubs__0__RefreshToken: "initial-refresh-token"
      Customers__0__Hubs__0__TokenExpiresAt: "2026-04-01T00:00:00Z"

volumes:
  hpoll-data:
```

Alternatively, mount an `appsettings.Production.json` file instead of using
environment variables for the customer/hub config:

```yaml
services:
  hpoll:
    image: pkinerd/hpoll:latest
    container_name: hpoll
    restart: unless-stopped
    volumes:
      - hpoll-data:/app/data
      - ./appsettings.Production.json:/app/appsettings.Production.json:ro
    environment:
      AWS_ACCESS_KEY_ID: "your-aws-key"
      AWS_SECRET_ACCESS_KEY: "your-aws-secret"

volumes:
  hpoll-data:
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
    "SendTimeUtc": "08:00"
  },
  "Polling": {
    "IntervalMinutes": 60
  },
  "Customers": [
    {
      "name": "Jane Doe",
      "email": "jane@example.com",
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
inside the container). Mount the `/app/data` volume to persist data across
container restarts.
