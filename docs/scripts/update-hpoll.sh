#!/bin/bash
# update-hpoll.sh — Check for and apply Docker image updates on Synology NAS.
#
# Usage:
#   ./update-hpoll.sh              # Check & update the default project (hpoll-dev, tag: dev)
#   ./update-hpoll.sh --check      # Check only, don't update
#   ./update-hpoll.sh --tag latest # Use a different image tag
#   ./update-hpoll.sh --project hpoll-prod --tag latest --compose-dir /volume1/Data/hpoll/prod/server
#
# How it works:
#   1. Records the current image ID for each image
#   2. Pulls the latest version from the registry
#   3. Compares the new image ID to the old one
#   4. If any image changed, restarts the compose project
#
# This avoids fragile digest comparison between local RepoDigests and remote
# manifest digests, which can differ even when images are identical.

set -euo pipefail

# ── Defaults ──────────────────────────────────────────────────────────────────
PROJECT="hpoll-dev"
TAG="dev"
COMPOSE_DIR="/volume1/Data/hpoll/dev/server"
CHECK_ONLY=false
IMAGES=("pkinerd/hpoll" "pkinerd/hpoll-admin")

# ── Parse arguments ───────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --check)       CHECK_ONLY=true; shift ;;
        --tag)         TAG="$2"; shift 2 ;;
        --project)     PROJECT="$2"; shift 2 ;;
        --compose-dir) COMPOSE_DIR="$2"; shift 2 ;;
        -h|--help)
            sed -n '2,/^$/s/^# \?//p' "$0"
            exit 0
            ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# ── Helpers ───────────────────────────────────────────────────────────────────
log()  { echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"; }
fail() { log "ERROR: $*" >&2; exit 1; }

get_image_id() {
    docker image inspect "$1:$TAG" --format '{{.Id}}' 2>/dev/null || echo "none"
}

# ── Pull images and detect changes ───────────────────────────────────────────
log "Project:  $PROJECT"
log "Tag:      $TAG"
log "Compose:  $COMPOSE_DIR"
echo

update_available=false

for img in "${IMAGES[@]}"; do
    old_id=$(get_image_id "$img")
    old_short="${old_id:0:19}"

    log "Pulling $img:$TAG..."
    docker pull -q "$img:$TAG" >/dev/null

    new_id=$(get_image_id "$img")
    new_short="${new_id:0:19}"

    if [[ "$old_id" == "none" ]]; then
        log "  $img:$TAG — newly pulled ($new_short)"
        update_available=true
    elif [[ "$old_id" != "$new_id" ]]; then
        log "  $img:$TAG — UPDATE AVAILABLE"
        log "    old: $old_short"
        log "    new: $new_short"
        update_available=true
    else
        log "  $img:$TAG — up to date ($new_short)"
    fi
done

echo

if [[ "$update_available" == false ]]; then
    log "All images are up to date. Nothing to do."
    exit 0
fi

if [[ "$CHECK_ONLY" == true ]]; then
    log "Update(s) available. Run without --check to apply."
    exit 0
fi

# ── Apply update ──────────────────────────────────────────────────────────────
COMPOSE_FILE="$COMPOSE_DIR/docker-compose.yml"
[[ -f "$COMPOSE_FILE" ]] || fail "Compose file not found: $COMPOSE_FILE"

log "Restarting project $PROJECT with new images..."
docker compose -p "$PROJECT" -f "$COMPOSE_FILE" up -d --force-recreate

echo
log "Update complete. Container status:"
docker compose -p "$PROJECT" -f "$COMPOSE_FILE" ps
