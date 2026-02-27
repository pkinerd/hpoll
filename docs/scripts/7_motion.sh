#!/bin/bash
# get_motion.sh

ACCESS_TOKEN="$( cat ../keys/bridges/test_bridge_001/access_token )"
APP_KEY="$( cat ../keys/bridges/test_bridge_001/username )"
BASE_URL="https://api.meethue.com/route"

# Get all motion sensors
curl -X GET "$BASE_URL/clip/v2/resource/motion" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "hue-application-key: $APP_KEY"