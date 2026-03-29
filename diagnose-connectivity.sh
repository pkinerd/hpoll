#!/usr/bin/env bash
# Diagnostic script for Hue device connectivity status.
# Requires: ACCESS_TOKEN and HUE_APPLICATION_KEY environment variables.
#
# Usage:
#   export ACCESS_TOKEN="your-oauth-token"
#   export HUE_APPLICATION_KEY="your-app-key"
#   bash diagnose-connectivity.sh

set -euo pipefail

BASE_URL="https://api.meethue.com/route/clip/v2"

if [[ -z "${ACCESS_TOKEN:-}" ]]; then
    echo "ERROR: ACCESS_TOKEN environment variable is not set." >&2
    exit 1
fi

if [[ -z "${HUE_APPLICATION_KEY:-}" ]]; then
    echo "ERROR: HUE_APPLICATION_KEY environment variable is not set." >&2
    exit 1
fi

hue_get() {
    local path="$1"
    curl -s -w "\n%{http_code}" \
        -H "Authorization: Bearer ${ACCESS_TOKEN}" \
        -H "hue-application-key: ${HUE_APPLICATION_KEY}" \
        "${BASE_URL}${path}"
}

parse_response() {
    local response="$1"
    local label="$2"
    local body http_code

    http_code=$(echo "$response" | tail -n1)
    body=$(echo "$response" | sed '$d')

    if [[ "$http_code" != "200" ]]; then
        echo "ERROR: ${label} returned HTTP ${http_code}" >&2
        echo "$body" | head -20 >&2
        return 1
    fi
    echo "$body"
}

echo "=== 1. Fetching devices ==="
devices_raw=$(hue_get "/resource/device")
devices=$(parse_response "$devices_raw" "devices") || exit 1
device_count=$(echo "$devices" | jq '.data | length')
echo "Found ${device_count} devices"
echo ""

# Build device name lookup: id -> name
echo "$devices" | jq -r '.data[] | "\(.id)\t\(.metadata.name)"' | while IFS=$'\t' read -r id name; do
    echo "  Device: ${name} (${id})"
done
echo ""

echo "=== 2. Fetching zigbee_connectivity ==="
connectivity_raw=$(hue_get "/resource/zigbee_connectivity")
connectivity=$(parse_response "$connectivity_raw" "zigbee_connectivity") || exit 1
conn_count=$(echo "$connectivity" | jq '.data | length')
echo "Found ${conn_count} zigbee_connectivity resources"
echo ""

# Show each connectivity resource with device name
echo "$connectivity" | jq -r '.data[] | "\(.owner.rid)\t\(.status)\t\(.mac_address // "n/a")\t\(.id)"' | while IFS=$'\t' read -r owner_rid status mac conn_id; do
    device_name=$(echo "$devices" | jq -r --arg rid "$owner_rid" '.data[] | select(.id == $rid) | .metadata.name // "UNKNOWN"')
    if [[ "$status" != "connected" ]]; then
        marker="  *** UNREACHABLE ***"
    else
        marker=""
    fi
    echo "  ${device_name}: status=${status}, mac=${mac}, owner=${owner_rid}${marker}"
done
echo ""

echo "=== 3. Fetching device_power (battery) ==="
battery_raw=$(hue_get "/resource/device_power")
battery=$(parse_response "$battery_raw" "device_power") || exit 1
bat_count=$(echo "$battery" | jq '.data | length')
echo "Found ${bat_count} device_power resources"
echo ""

echo "$battery" | jq -r '.data[] | "\(.owner.rid)\t\(.power_state.battery_level // "n/a")\t\(.power_state.battery_state // "n/a")"' | while IFS=$'\t' read -r owner_rid level state; do
    device_name=$(echo "$devices" | jq -r --arg rid "$owner_rid" '.data[] | select(.id == $rid) | .metadata.name // "UNKNOWN"')
    echo "  ${device_name}: battery=${level}%, state=${state}, owner=${owner_rid}"
done
echo ""

echo "=== 4. Summary: Unreachable Devices ==="
unreachable=$(echo "$connectivity" | jq -r '.data[] | select(.status != "connected") | "\(.owner.rid)\t\(.status)"')
if [[ -z "$unreachable" ]]; then
    echo "  All devices connected."
else
    echo "$unreachable" | while IFS=$'\t' read -r owner_rid status; do
        device_name=$(echo "$devices" | jq -r --arg rid "$owner_rid" '.data[] | select(.id == $rid) | .metadata.name // "UNKNOWN"')
        echo "  ${device_name}: ${status} (owner.rid=${owner_rid})"
    done
fi
echo ""

echo "=== 5. Cross-reference: devices with connectivity vs battery ==="
echo "Checking which owner.rids appear in zigbee_connectivity but NOT in device_power..."
conn_owners=$(echo "$connectivity" | jq -r '.data[].owner.rid' | sort -u)
bat_owners=$(echo "$battery" | jq -r '.data[].owner.rid' | sort -u)

comm -23 <(echo "$conn_owners") <(echo "$bat_owners") | while read -r rid; do
    device_name=$(echo "$devices" | jq -r --arg rid "$rid" '.data[] | select(.id == $rid) | .metadata.name // "UNKNOWN"')
    conn_status=$(echo "$connectivity" | jq -r --arg rid "$rid" '.data[] | select(.owner.rid == $rid) | .status')
    echo "  ${device_name}: has connectivity (${conn_status}) but NO battery entry — owner.rid=${rid}"
done

echo ""
echo "Checking which owner.rids appear in device_power but NOT in zigbee_connectivity..."
comm -23 <(echo "$bat_owners") <(echo "$conn_owners") | while read -r rid; do
    device_name=$(echo "$devices" | jq -r --arg rid "$rid" '.data[] | select(.id == $rid) | .metadata.name // "UNKNOWN"')
    echo "  ${device_name}: has battery but NO connectivity entry — owner.rid=${rid}"
done

echo ""
echo "Done. Share the output above so I can identify what's mismatched."
