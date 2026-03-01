---
name: setup-environment-dotnetsdk
description: Install the .NET 8.0 SDK in environments that route traffic through an egress proxy. Configures apt to use the proxy from GLOBAL_AGENT_HTTP_PROXY, then installs dotnet-sdk-8.0 via apt-get. Use when `dotnet` is not available and you need to build or test the project.
user_invocable: true
---

# Setup Environment — .NET SDK

## Instructions

Install the .NET 8.0 SDK so the project can be built and tested. This skill handles the common case where outbound traffic is routed through an egress proxy (e.g., in Claude Code web sessions) and apt cannot resolve hosts without proxy configuration.

### Step 1: Check if dotnet is already available

```bash
dotnet --version 2>/dev/null
```

- **If dotnet is available** and the version is 8.x: stop — inform the user the SDK is already installed. Do NOT reinstall.
- **If dotnet is not available** (command not found or wrong version): proceed to Step 2.

### Step 2: Configure apt to use the egress proxy

The environment variable `GLOBAL_AGENT_HTTP_PROXY` contains the proxy URL. This URL can be very large (several KB, due to embedded JWT tokens), so it cannot be reliably passed through shell variable expansion. Use Python to write the apt proxy configuration:

```bash
python3 -c "
import os
proxy = os.environ.get('GLOBAL_AGENT_HTTP_PROXY', '')
if not proxy:
    print('NO_PROXY_SET')
else:
    with open('/etc/apt/apt.conf.d/99proxy', 'w') as f:
        f.write(f'Acquire::http::Proxy \"{proxy}\";\n')
        f.write(f'Acquire::https::Proxy \"{proxy}\";\n')
    print('PROXY_CONFIGURED')
"
```

- **If output is `PROXY_CONFIGURED`**: proceed to Step 3.
- **If output is `NO_PROXY_SET`**: the proxy variable is not set. This may be fine in environments with direct internet access — proceed to Step 3 anyway, as apt may work without proxy config.

### Step 3: Install the .NET 8.0 SDK

```bash
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

If `apt-get update` fails with DNS resolution errors even after proxy configuration, inform the user that the network/proxy setup may need manual intervention.

### Step 4: Verify the installation

```bash
dotnet --version
```

Confirm the output shows an 8.x version (e.g., `8.0.xxx`).

### Step 5: Report to the user

Tell the user:

1. Whether the SDK was already installed or was freshly installed
2. The installed version
3. That they can now run `dotnet build`, `dotnet test`, etc.

## Example Flow

### Happy path (proxy environment)

```
User: /setup-environment-dotnetsdk

Claude:
1. Checks `dotnet --version` — command not found, proceeds
2. Reads GLOBAL_AGENT_HTTP_PROXY, writes /etc/apt/apt.conf.d/99proxy
3. Runs apt-get update && apt-get install -y dotnet-sdk-8.0
4. Verifies: dotnet --version → 8.0.114
5. Reports: ".NET SDK 8.0.114 installed successfully. You can now build and test."
```

### Already installed

```
User: /setup-environment-dotnetsdk

Claude:
1. Checks `dotnet --version` → 8.0.114
2. Reports: ".NET SDK 8.0.114 is already installed. No action needed."
```

## Important Notes

- The proxy URL in `GLOBAL_AGENT_HTTP_PROXY` can be several KB due to embedded tokens — always use Python (not shell echo/printf) to write it to the apt config file to avoid truncation or quoting issues.
- The `/etc/apt/apt.conf.d/99proxy` file is written with `sudo`-less Python because the process typically has write access. If permission is denied, retry with `sudo`.
- This skill only installs the SDK via apt. It does not use the `dotnet-install.sh` script, which may fail behind proxies that reject direct curl/wget requests.
