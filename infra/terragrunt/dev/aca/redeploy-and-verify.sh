#!/usr/bin/env bash
#
# Roll the latest `main` images onto the ACA dev stack and verify the device-registration path.
#
# Why this exists: the backend container apps pin the `:latest` image *tag*, and CI republishes
# `:latest` on every merge to main — but a `terragrunt apply` won't necessarily roll a new revision
# (the tag string is unchanged), so the apps keep running the previously-pulled digest. This script
# forces each app to a fresh revision (which re-resolves `:latest`) and then verifies registration
# works end-to-end — in particular that the cold-IdentityManager quota-check retry (#429) is live.
#
# It only uses `az containerapp` + `curl`; it does NOT run terraform/terragrunt. Run it from a host
# that is `az login`-ed to the dev subscription.
#
#   RESOURCE_GROUP=<rg> ./redeploy-and-verify.sh            # roll + verify
#   RESOURCE_GROUP=<rg> DEPLOY=0 ./redeploy-and-verify.sh   # verify only (no roll)
#   RESOURCE_GROUP=<rg> ASSUME_YES=1 ./redeploy-and-verify.sh
#
set -euo pipefail

# ── Config (override via env) ───────────────────────────────────────────────
RESOURCE_GROUP="${RESOURCE_GROUP:-}"   # REQUIRED. Discover: az containerapp list -o table
GATEWAY_URL="${GATEWAY_URL:-https://sb-ca-apigateway-dev.whitesea-94fe530f.northeurope.azurecontainerapps.io}"
GHCR_ORG="${GHCR_ORG:-signalbeam-io}"
TENANT_ID="${TENANT_ID:-00000000-0000-0000-0000-000000000001}"
DEPLOY="${DEPLOY:-1}"                   # 0 = verify only
ASSUME_YES="${ASSUME_YES:-0}"
REVISION_SUFFIX="r$(date +%Y%m%d%H%M%S)"

# app-name : ghcr image name. DeviceManager carries the #425/#426/#429 fixes.
SERVICES=(
  "sb-ca-apigateway-dev:apigateway"
  "sb-ca-identitymanager-dev:identitymanager"
  "sb-ca-devicemanager-dev:devicemanager"
  "sb-ca-bundleorchestrator-dev:bundleorchestrator"
  "sb-ca-telemetryprocessor-dev:telemetryprocessor"
)

log()  { printf '\033[1;34m▶ %s\033[0m\n' "$*"; }
ok()   { printf '\033[1;32m✓ %s\033[0m\n' "$*"; }
warn() { printf '\033[1;33m! %s\033[0m\n' "$*"; }
die()  { printf '\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }
curlh(){ curl -sS --http1.1 "$@"; }   # HTTP/1.1 — the ACA ingress can throw HTTP/2 framing errors

# ── Pre-flight ──────────────────────────────────────────────────────────────
preflight() {
  command -v az   >/dev/null || die "az CLI not found"
  command -v curl >/dev/null || die "curl not found"
  [[ -n "$RESOURCE_GROUP" ]]  || die "Set RESOURCE_GROUP (discover: az containerapp list -o table)"
  local sub; sub="$(az account show --query name -o tsv 2>/dev/null)" || die "Not logged in: run 'az login'"
  ok "Subscription: $sub"
  az group show -n "$RESOURCE_GROUP" >/dev/null 2>&1 || die "Resource group '$RESOURCE_GROUP' not found"
  ok "Resource group: $RESOURCE_GROUP"
}

confirm() {
  [[ "$ASSUME_YES" == "1" ]] && return 0
  read -r -p "$1 [y/N] " a; [[ "$a" == "y" || "$a" == "Y" ]]
}

# ── Deploy: force a fresh revision so each app re-pulls :latest ──────────────
deploy() {
  log "Rolling ${#SERVICES[@]} apps to a new revision (suffix: $REVISION_SUFFIX)"
  confirm "Update container apps in '$RESOURCE_GROUP' to latest :latest images?" || die "Aborted."
  for entry in "${SERVICES[@]}"; do
    local app="${entry%%:*}" img="${entry##*:}"
    local image="ghcr.io/${GHCR_ORG}/${img}:latest"
    log "  $app  <-  $image"
    az containerapp update \
      --name "$app" --resource-group "$RESOURCE_GROUP" \
      --image "$image" --revision-suffix "$REVISION_SUFFIX" \
      --output none \
      || die "Update failed for $app"
  done
  ok "All apps updated; waiting for new revisions to run."
  for entry in "${SERVICES[@]}"; do wait_running "${entry%%:*}"; done
}

wait_running() {
  local app="$1" rev="$1--$REVISION_SUFFIX" state="" i=0
  for ((i=0; i<40; i++)); do
    state="$(az containerapp revision show -n "$app" -g "$RESOURCE_GROUP" --revision "$rev" \
             --query "properties.runningState" -o tsv 2>/dev/null || true)"
    case "$state" in
      Running|RunningAtMaxScale) ok "  $app: $state"; return 0 ;;
      Failed|Degraded)          die "  $app revision $state — check: az containerapp logs show -n $app -g $RESOURCE_GROUP" ;;
    esac
    sleep 6
  done
  warn "  $app: still '$state' after timeout (scale-to-zero apps may read 'Stopped' until first request)"
}

# ── Verify ──────────────────────────────────────────────────────────────────
verify() {
  log "Gateway health"
  [[ "$(curlh -o /dev/null -w '%{http_code}' "$GATEWAY_URL/health")" == "200" ]] \
    && ok "  /health 200" || die "  gateway not healthy"

  log "Auth enforced on a protected endpoint"
  [[ "$(curlh -o /dev/null -w '%{http_code}' "$GATEWAY_URL/api/devices")" == "401" ]] \
    && ok "  GET /api/devices -> 401" || warn "  unexpected status on GET /api/devices"

  # Wake IdentityManager so the warm-path registration is fast (the cold path is tested separately).
  log "Warming IdentityManager"
  curlh -o /dev/null "$GATEWAY_URL/api/subscriptions" || true

  log "Registration round-trip (warm)"
  local body code
  body="$(curlh -X POST "$GATEWAY_URL/api/devices" \
            -H 'Content-Type: application/json' \
            -d "{\"name\":\"deploy-verify-$(date +%s)\",\"tenantId\":\"$TENANT_ID\"}" \
            -w $'\n%{http_code}')"
  code="$(tail -n1 <<<"$body")"; body="$(sed '$d' <<<"$body")"
  if [[ "$code" == "201" ]]; then
    ok "  POST /api/devices -> 201 ($(tr -d '\n' <<<"$body"))"
  else
    die "  POST /api/devices -> $code: $body"
  fi

  cat <<EOF

$(ok "Deploy verified: apps on the new revision and registration works.")

To specifically confirm the #429 cold-start retry is live, exercise the COLD path:
  1. Let IdentityManager idle to zero (a few minutes of no traffic), OR scale it down:
       az containerapp update -n sb-ca-identitymanager-dev -g "$RESOURCE_GROUP" --min-replicas 0
  2. Without warming it, register a device:
       curl -sS --http1.1 -X POST "$GATEWAY_URL/api/devices" \\
         -H 'Content-Type: application/json' \\
         -d '{"name":"cold-probe","tenantId":"$TENANT_ID"}' -w '\\n%{http_code}\\n'
  3. Expect 201 (the retry woke IdentityManager). Before #429 this returned
     400 {"error":"QUOTA_CHECK_ERROR"} after ~10s.
EOF
}

# ── Main ────────────────────────────────────────────────────────────────────
preflight
[[ "$DEPLOY" == "1" ]] && deploy || warn "DEPLOY=0 — skipping roll, verifying current state"
verify
