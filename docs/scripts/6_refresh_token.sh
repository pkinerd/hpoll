#!/bin/bash
# refresh_token.sh

CLIENT_ID="$( cat ../keys/hue_app_client_id )"
CLIENT_SECRET="$( cat ../keys/hue_app_client_secret )"

REFRESH_TOKEN="$( cat ../keys/bridges/test_bridge_001/refresh_token )"  # from step 1 response

CREDENTIALS=$(echo -n "$CLIENT_ID:$CLIENT_SECRET" | base64 | tr -d '\n' )

curl -v4 -X POST "https://api.meethue.com/v2/oauth2/token" \
  -H "Authorization: Basic $CREDENTIALS" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=refresh_token&refresh_token=$REFRESH_TOKEN"