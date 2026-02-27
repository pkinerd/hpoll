#!/bin/bash
# motion_table.sh

ACCESS_TOKEN="$( cat ../keys/bridges/test_bridge_001/access_token )"
APP_KEY="$( cat ../keys/bridges/test_bridge_001/username )"
BASE_URL="https://api.meethue.com/route"

# Fetch both endpoints
MOTION=$(curl -s -X GET "$BASE_URL/clip/v2/resource/motion" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "hue-application-key: $APP_KEY")

DEVICES=$(curl -s -X GET "$BASE_URL/clip/v2/resource/device" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "hue-application-key: $APP_KEY")

# Replace everything after the DEVICES= block with this:

{
  echo "ID|NAME|UTC TIME|LOCAL TIME"
  echo "--|----|---------|-----------"
  jq -r --argjson devices "$DEVICES" '
    .data[] |
    . as $motion |
    ($devices.data[] | select(.services[]? | .rid == $motion.id)) as $device |
    "\($motion.id)|\($device.metadata.name)|\($motion.motion.motion_report.changed)"
  ' <<< "$MOTION" | tr -d '\r' | while IFS='|' read -r id name timestamp; do
    clean_ts="${timestamp%.*}"
    local_time=$(date -d "${clean_ts}Z" "+%a %b %d %Y %I:%M:%S %p" 2>/dev/null)
    if [ -z "$local_time" ]; then
      local_time=$(date -j -f "%Y-%m-%dT%H:%M:%S" "$clean_ts" "+%a %b %d %Y %I:%M:%S %p" 2>/dev/null)
    fi
    [ -z "$local_time" ] && local_time="N/A"
    echo "${id}|${name}|${timestamp}|${local_time}"
  done
} | column -t -s '|'