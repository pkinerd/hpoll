#!/bin/bash
# token_exchange.sh

CLIENT_ID="$( cat ../keys/hue_app_client_id )"
CLIENT_SECRET="$( cat ../keys/hue_app_client_secret )"

REDIRECT_URI="https://localhost:3000/oauth"
AUTH_CODE="$1"  # grab this from the ?code= param in your redirect URL

CREDENTIALS=$(echo -n "$CLIENT_ID:$CLIENT_SECRET" | base64 | tr -d '\n' )

curl -X POST "https://api.meethue.com/v2/oauth2/token" \
  -H "Authorization: Basic $CREDENTIALS" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code&code=$AUTH_CODE&redirect_uri=$REDIRECT_URI"