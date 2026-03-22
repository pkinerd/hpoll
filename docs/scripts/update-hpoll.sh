#!/bin/bash
# update-hpoll.sh — Check for and apply Docker image updates on Synology NAS.
#
# Usage:
#   ./update-hpoll.sh              # Check & update the default project (hpoll-dev, tag: dev)
#   ./update-hpoll.sh --check      # Check only, don't update
#   ./update-hpoll.sh --tag latest # Use a different image tag
#   ./update-hpoll.sh --project hpoll-prod --tag latest --compose-dir /volume1/Data/hpoll/prod/server
#
# Synology Container Manager stores projects and uses docker compose under the hood.
# To update, we stop the project, remove containers and old images, pull the new
# images, and rebuild/start the project.

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

get_local_digest() {
    local img="$1:$TAG"
    docker image inspect "$img" --format '{{index .RepoDigests 0}}' 2>/dev/null \
        | sed 's/.*@//' || echo "not-pulled"
}

get_remote_digest() {
    local img="$1"
    # Use docker manifest inspect to get the remote digest without pulling.
    # This requires experimental CLI features on older Docker versions; on
    # Synology DSM 7.2+ the bundled Docker supports this natively.
    docker manifest inspect "docker.io/$img:$TAG" 2>/dev/null \
        | grep -m1 '"digest"' | sed 's/.*"digest": *"//;s/".*//' || echo "unknown"
}

# ── Check current vs remote versions ─────────────────────────────────────────
log "Project:  $PROJECT"
log "Tag:      $TAG"
log "Compose:  $COMPOSE_DIR"
echo

update_available=false

for img in "${IMAGES[@]}"; do
    local_digest=$(get_local_digest "$img")
    remote_digest=$(get_remote_digest "$img")

    local_short="${local_digest:0:16}"
    remote_short="${remote_digest:0:16}"

    if [[ "$local_digest" == "not-pulled" ]]; then
        log "$img:$TAG — not present locally (will pull)"
        update_available=true
    elif [[ "$remote_digest" == "unknown" ]]; then
        log "$img:$TAG — local: $local_short | remote: could not check"
    elif [[ "$local_digest" != "$remote_digest" ]]; then
        log "$img:$TAG — UPDATE AVAILABLE"
        log "  local:  $local_short"
        log "  remote: $remote_short"
        update_available=true
    else
        log "$img:$TAG — up to date ($local_short)"
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

log "Stopping project $PROJECT..."
docker compose -p "$PROJECT" -f "$COMPOSE_FILE" down --remove-orphans

log "Removing old images..."
for img in "${IMAGES[@]}"; do
    docker image rm "$img:$TAG" 2>/dev/null && log "  Removed $img:$TAG" || log "  $img:$TAG not present (skipped)"
done

log "Pulling latest images..."
for img in "${IMAGES[@]}"; do
    docker pull "$img:$TAG"
done

log "Starting project $PROJECT..."
docker compose -p "$PROJECT" -f "$COMPOSE_FILE" up -d

echo
log "Update complete. Container status:"
docker compose -p "$PROJECT" -f "$COMPOSE_FILE" ps
