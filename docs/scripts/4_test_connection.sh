#!/bin/bash
# test_connection.sh

ACCESS_TOKEN="$( cat ../keys/bridges/test_bridge_001/access_token )"  # from step 1 response
HUE_USERNAME="$( cat ../keys/bridges/test_bridge_001/username )"  # the "username" value returned in finalize_auth

curl -X GET "https://api.meethue.com/route/clip/v2/resource/device" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "hue-application-key: $HUE_USERNAME"
