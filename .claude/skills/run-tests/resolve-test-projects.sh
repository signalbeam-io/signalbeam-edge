#!/usr/bin/env bash
# resolve-test-projects.sh — Map changed files to affected test project paths.
#
# Usage: resolve-test-projects.sh [--mode unit|integration|all] [--service slug] [--diff-base ref]
#
# Exit codes:
#   0 — found targeted projects (paths on stdout)
#   1 — error
#   2 — run full suite (shared dependency changed or fallback)

set -euo pipefail

MODE="unit"
SERVICE=""
DIFF_BASE="main"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode)       MODE="$2"; shift 2 ;;
    --service)    SERVICE="$2"; shift 2 ;;
    --diff-base)  DIFF_BASE="$2"; shift 2 ;;
    *)            echo "Unknown arg: $1" >&2; exit 1 ;;
  esac
done

REPO_ROOT="$(git rev-parse --show-toplevel)"

# --- Service alias normalization ---
normalize_service() {
  local input
  input="$(printf '%s' "$1" | tr '[:upper:]' '[:lower:]')"
  case "$input" in
    dm|devicemanager|device-manager)                   echo "device-manager" ;;
    bo|bundleorchestrator|bundle-orchestrator)          echo "bundle-orchestrator" ;;
    tp|telemetryprocessor|telemetry-processor)          echo "telemetry-processor" ;;
    ea|edgeagent|edge-agent)                            echo "edge-agent" ;;
    im|identitymanager|identity-manager)                echo "identity-manager" ;;
    domain)                                             echo "domain" ;;
    shared-infra|shared-infrastructure|sharedinfra)     echo "shared-infra" ;;
    service-defaults|servicedefaults)                   echo "service-defaults" ;;
    *)                                                  echo "" ;;
  esac
}

# --- Test project mappings ---
unit_tests_for() {
  case "$1" in
    domain)
      echo "tests/SignalBeam.Domain.Tests"
      echo "src/tests/SignalBeam.Domain.Tests"
      ;;
    shared-infra)
      echo "tests/SignalBeam.Shared.Infrastructure.Tests"
      echo "src/tests/SignalBeam.Shared.Infrastructure.Tests"
      ;;
    device-manager)
      echo "tests/SignalBeam.DeviceManager.Tests.Unit"
      ;;
    bundle-orchestrator)
      echo "tests/SignalBeam.BundleOrchestrator.Tests.Unit"
      echo "src/tests/SignalBeam.BundleOrchestrator.Application.Tests"
      ;;
    telemetry-processor)
      echo "tests/SignalBeam.TelemetryProcessor.Infrastructure.Tests"
      ;;
    edge-agent)
      echo "tests/SignalBeam.EdgeAgent.Tests.Unit"
      ;;
    identity-manager)
      echo "tests/SignalBeam.IdentityManager.Application.Tests"
      ;;
  esac
}

integration_tests_for() {
  case "$1" in
    device-manager)
      echo "tests/SignalBeam.DeviceManager.Tests.Integration"
      ;;
    bundle-orchestrator)
      echo "src/tests/SignalBeam.BundleOrchestrator.Integration.Tests"
      ;;
    telemetry-processor)
      echo "tests/SignalBeam.TelemetryProcessor.Tests.Integration"
      ;;
    edge-agent)
      echo "tests/SignalBeam.EdgeAgent.Tests.Integration"
      ;;
  esac
}

emit_tests_for_service() {
  local svc="$1"
  if [[ "$MODE" == "unit" || "$MODE" == "all" ]]; then
    unit_tests_for "$svc"
  fi
  if [[ "$MODE" == "integration" || "$MODE" == "all" ]]; then
    integration_tests_for "$svc"
  fi
}

# Filter to paths that actually exist
filter_existing() {
  while IFS= read -r p; do
    [[ -d "$REPO_ROOT/$p" ]] && echo "$p"
  done
}

# --- Explicit service mode ---
if [[ -n "$SERVICE" ]]; then
  NORMALIZED="$(normalize_service "$SERVICE")"
  if [[ -z "$NORMALIZED" ]]; then
    echo "Unknown service: $SERVICE" >&2
    exit 1
  fi
  # Shared projects → emit their own tests if they exist, otherwise full suite
  if [[ "$NORMALIZED" == "domain" || "$NORMALIZED" == "shared-infra" || "$NORMALIZED" == "service-defaults" ]]; then
    RESULTS="$(emit_tests_for_service "$NORMALIZED" | filter_existing)"
    if [[ -n "$RESULTS" ]]; then
      echo "$RESULTS"
      exit 0
    fi
    exit 2
  fi
  RESULTS="$(emit_tests_for_service "$NORMALIZED" | filter_existing)"
  if [[ -n "$RESULTS" ]]; then
    echo "$RESULTS"
    exit 0
  fi
  echo "No test projects found for $SERVICE (mode=$MODE)" >&2
  exit 1
fi

# --- Auto-detect mode: gather changed files ---
CHANGED_FILES="$(
  {
    git diff --name-only "${DIFF_BASE}"...HEAD 2>/dev/null || true
    git diff --name-only 2>/dev/null || true
    git diff --name-only --cached 2>/dev/null || true
  } | sort -u
)"

if [[ -z "$CHANGED_FILES" ]]; then
  # No changes detected — fallback to full suite
  exit 2
fi

# --- Map files to service keys ---
SERVICES=()
HAS_SOURCE_CHANGES=false
DIRECT_TEST_DIRS=()

while IFS= read -r file; do
  case "$file" in
    src/Shared/SignalBeam.Domain/*)           exit 2 ;;  # foundational — run all
    src/Shared/SignalBeam.Shared.Infrastructure/*) exit 2 ;;
    src/SignalBeam.ServiceDefaults/*)         exit 2 ;;
    src/DeviceManager/*)                      SERVICES+=("device-manager"); HAS_SOURCE_CHANGES=true ;;
    src/BundleOrchestrator/*)                 SERVICES+=("bundle-orchestrator"); HAS_SOURCE_CHANGES=true ;;
    src/TelemetryProcessor/*)                 SERVICES+=("telemetry-processor"); HAS_SOURCE_CHANGES=true ;;
    src/EdgeAgent/*)                          SERVICES+=("edge-agent"); HAS_SOURCE_CHANGES=true ;;
    src/IdentityManager/*)                    SERVICES+=("identity-manager"); HAS_SOURCE_CHANGES=true ;;
    tests/*)
      # Direct test file change — resolve to its project dir
      dir="tests/$(echo "$file" | cut -d/ -f2)"
      DIRECT_TEST_DIRS+=("$dir")
      HAS_SOURCE_CHANGES=true
      ;;
    src/tests/*)
      dir="src/tests/$(echo "$file" | cut -d/ -f3)"
      DIRECT_TEST_DIRS+=("$dir")
      HAS_SOURCE_CHANGES=true
      ;;
    src/*)
      # Unknown src path — fallback
      exit 2
      ;;
    # Non-source files (docs, deploy, .claude, etc.) — ignored
  esac
done <<< "$CHANGED_FILES"

if [[ "$HAS_SOURCE_CHANGES" == false ]]; then
  # Only non-source files changed
  echo "NO_TESTS_AFFECTED"
  exit 0
fi

# Deduplicate services and collect test projects
RESULTS="$(
  {
    printf '%s\n' "${SERVICES[@]}" | sort -u | while IFS= read -r svc; do
      [[ -n "$svc" ]] && emit_tests_for_service "$svc"
    done
    printf '%s\n' "${DIRECT_TEST_DIRS[@]}"
  } | sort -u | filter_existing
)"

if [[ -z "$RESULTS" ]]; then
  exit 2
fi

echo "$RESULTS"
exit 0
