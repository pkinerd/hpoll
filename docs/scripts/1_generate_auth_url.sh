#!/bin/bash
# generate_auth_url.sh

CLIENT_ID="$( cat ../keys/hue_app_client_id )"

REDIRECT_URI="https://localhost:3000/oauth"
# REDIRECT_URI="data:text/plain," does not work

STATE=$(openssl rand -hex 16)  # random state for CSRF protection

AUTH_URL="https://api.meethue.com/v2/oauth2/authorize?client_id=${CLIENT_ID}&response_type=code&state=${STATE}&redirect_uri=${REDIRECT_URI}"

echo ""
echo "Open this URL in your browser:"
echo ""
echo "$AUTH_URL"
echo ""
echo "State value (save this if you want to verify it later): $STATE"
echo ""