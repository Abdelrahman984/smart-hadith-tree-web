#!/usr/bin/env bash
# deploy-remote — production deploy. Runs ON the host (/opt/smart-hadith-tree), never locally.
#
# Called by deploy/ssh-deploy-gate.sh after it checked out the new commit, with the previously live commit
# as $1. A hand run (no $1) does the full deploy:
#     ssh root@169.58.190.201 'cd /opt/smart-hadith-tree && ./deploy/deploy-remote.sh'
#
# Order: skip if nothing runtime changed -> back up the database if a migration is coming -> keep the
# current images as :previous -> build -> up -> prove it (local + public checks) -> on any failure, put the
# previous images and commit back.
#
# Never touches: .env, compose.edge.yml, the pgdata volume (nor the old sqldata one), the edge Caddy, or any
# non-smart-hadith-tree container. No `docker system prune` / `down -v`: this is a shared host (devops-handoff.md §2).
# A change of database image (SQL Server -> PostgreSQL, or a PostgreSQL major version) is a cutover, not a deploy:
# it is refused here and done by hand (devops-handoff.md, "Cutover to PostgreSQL").
set -euo pipefail
cd /opt/smart-hadith-tree

BEFORE="${1:-}"
AFTER="$(git rev-parse HEAD)"
START="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
log() { echo "deploy-remote: $*"; }
envval() { grep -E "^$1=" .env | tail -1 | cut -d= -f2- || true; }

# .env sets COMPOSE_FILE=docker-compose.prod.yml:compose.edge.yml, so plain `docker compose` is the prod stack.
grep -q '^COMPOSE_FILE=' .env || { log ".env has no COMPOSE_FILE, refusing to guess the stack"; exit 1; }

API_PORT="$(envval API_PORT)"; API_PORT="${API_PORT:-5147}"
WEB_PORT="$(envval WEB_PORT)"; WEB_PORT="${WEB_PORT:-3100}"
DOMAIN="$(envval WEB_DOMAIN)"
SERVICES=(api web)

log "deploying ${BEFORE:-unknown} -> $AFTER"

# --- 0. Refuse a database image change. `up -d` would start the new image on an empty volume, the api would
# create an empty schema, and /api/books would still answer 200. Nothing has been touched yet, so put the
# checkout back and stop; the running stack keeps serving.
running_db="$(docker inspect -f '{{.Config.Image}}' "$(docker compose ps -q db 2>/dev/null)" 2>/dev/null || true)"
wanted_db="$(docker compose config --images 2>/dev/null | grep -m1 -E '^(postgres:|mcr\.microsoft\.com/mssql/)' || true)"
if [ -n "$running_db" ] && [ "$running_db" != "$wanted_db" ]; then
  log "database image would change ($running_db -> ${wanted_db:-?}): that is a cutover, see deploy/devops-handoff.md"
  [ -n "$BEFORE" ] && git reset --quiet --hard "$BEFORE"
  log "refused, ${BEFORE:-the checkout} left live"
  exit 1
fi

# --- 1. Skip the rebuild when only prose / pipeline files changed (health checks still run). ---------------
SKIP_BUILD=0
NON_RUNTIME='(\.md$|\.pdf$|\.pptx$|^docs/|^\.github/|^responses-evaluation/|^experiments/|^scripts/)'
CHANGED=""
if [ -n "$BEFORE" ] && [ "$BEFORE" != "$AFTER" ]; then
  CHANGED="$(git diff --name-only "$BEFORE" "$AFTER")"
  if ! printf '%s\n' "$CHANGED" | grep -qvE "$NON_RUNTIME"; then
    log "only docs/pipeline files changed, no rebuild"
    SKIP_BUILD=1
  fi
elif [ "$BEFORE" = "$AFTER" ]; then
  log "already at $AFTER, no rebuild"
  SKIP_BUILD=1
fi

rollback() {
  log "FAILED, rolling back to ${BEFORE:-<none>}"
  if [ "$SKIP_BUILD" = 0 ]; then
    for s in "${SERVICES[@]}"; do
      docker image inspect "smart-hadith-tree-$s:previous" >/dev/null 2>&1 &&
        docker image tag "smart-hadith-tree-$s:previous" "smart-hadith-tree-$s:latest"
    done
    docker compose up -d --no-build "${SERVICES[@]}" || true
  fi
  [ -n "$BEFORE" ] && git reset --quiet --hard "$BEFORE"
  log "rolled back. NOTE: a migration that already ran is NOT undone; restore backups/predeploy_*.dump if needed" \
      "(docker compose exec -T db pg_restore -U sht -d smart_hadith_tree --clean --if-exists /backups/<file>)."
}
trap rollback ERR

if [ "$SKIP_BUILD" = 0 ]; then
  # --- 2. Pre-deploy backup when the schema may change (api applies EF migrations on start). -----------------
  if [ -z "$BEFORE" ] || printf '%s\n' "$CHANGED" | grep -q '^src/SmartHadithTree.Infrastructure/Data/Migrations/'; then
    BAK="predeploy_$(date -u +%Y%m%dT%H%M%SZ)_${AFTER:0:7}.dump"
    log "migrations may run, backing up the database to backups/$BAK"
    # Custom format (-Fc): compressed, and pg_restore can restore it table by table.
    docker compose exec -T -e BAK="/backups/$BAK" db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f "$BAK"'
    # Keep the three newest pre-deploy backups.
    ls -1t backups/predeploy_*.dump 2>/dev/null | tail -n +4 | xargs -r rm -f
  fi

  # --- 3. Keep the running images so a failed deploy can go straight back. ----------------------------------
  for s in "${SERVICES[@]}"; do
    old="$(docker image inspect -f '{{.Id}}' "smart-hadith-tree-$s:previous" 2>/dev/null || true)"
    if docker image inspect "smart-hadith-tree-$s:latest" >/dev/null 2>&1; then
      docker image tag "smart-hadith-tree-$s:latest" "smart-hadith-tree-$s:previous"
    fi
    # The image that was :previous before this deploy is now untagged; remove only that one.
    [ -n "$old" ] && docker image rm "$old" >/dev/null 2>&1 || true
  done

  # --- 4. Build, then recreate. A failed build stops here with the old containers still serving. -----------
  docker compose build "${SERVICES[@]}"
  docker compose up -d
fi

# --- 5. Prove it. ----------------------------------------------------------------------------------------
wait_200() { # url, seconds
  for _ in $(seq 1 "$2"); do
    [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$1")" = 200 ] && return 0
    sleep 1
  done
  log "no 200 from $1 after $2 s"; return 1
}
wait_200 "http://127.0.0.1:$API_PORT/api/books" 180
# An empty database answers 200 too (the api creates the schema on start): the corpus must be there.
[ "$(curl -s --max-time 10 "http://127.0.0.1:$API_PORT/api/books")" != "[]" ] || { log "/api/books is empty: no corpus in the database"; false; }
wait_200 "http://127.0.0.1:$WEB_PORT/" 120
if docker compose logs --since "$START" api 2>&1 | grep -qi "migration check failed"; then
  log "api logged 'Database migration check failed'"; false
fi
if [ -n "$DOMAIN" ]; then
  wait_200 "https://$DOMAIN/" 30
  wait_200 "https://$DOMAIN/api/books" 30
  code="$(curl -s -o /dev/null -w '%{http_code}' "https://$DOMAIN/api/admin/import-gawami")"
  [ "$code" = 404 ] || { log "/api/admin answered $code, expected 404"; false; }
fi

trap - ERR
docker compose ps --format 'table {{.Name}}\t{{.Status}}'
log "OK, $AFTER is live"
