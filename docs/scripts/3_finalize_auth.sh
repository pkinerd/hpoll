#!/bin/bash
# finalize_auth.sh

ACCESS_TOKEN="$( cat ../keys/bridges/test_bridge_001/access_token )"  # from step 1 response


# The PUT /api/0/config {"linkbutton":true} call simulates pressing the physical link button on the Hue bridge.
# On a real local network setup, you'd have to physically press the button on the bridge before registering a new app — this is a security measure to prove you have physical access to the bridge. The API call replicates that action programmatically for the cloud API flow, essentially telling the bridge "yes, I authorise this app registration".
# It only needs to be done once, immediately before the POST /route/api call that creates your username/app key — the "pressed" state is only active briefly, which is why both calls are done back to back in the finalize_auth.sh script.

# Step A: Enable link button
curl -X PUT "https://api.meethue.com/route/api/0/config" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"linkbutton":true}'

echo ""

# Step B: Get your application key (username)
curl -X POST "https://api.meethue.com/route/api" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"devicetype":"my-hue-dev"}'