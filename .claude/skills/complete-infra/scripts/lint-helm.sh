#!/usr/bin/env bash
# Lint Helm charts that changed relative to origin/main.
# Runs helm lint + helm template on each changed chart.
#
# Usage: bash .claude/skills/complete-infra/scripts/lint-helm.sh

set -euo pipefail

HELM_CHANGED=$(git diff --name-only origin/main...HEAD -- 'deploy/charts/')

if [ -z "$HELM_CHANGED" ]; then
  echo "No Helm changes to lint"
  exit 0
fi

CHARTS=$(echo "$HELM_CHANGED" | cut -d'/' -f1-3 | sort -u)
FAILED=0

for chart in $CHARTS; do
  if [ ! -d "$chart" ]; then
    echo "SKIP: $chart (deleted or missing)"
    continue
  fi
  echo "=== Linting $chart ==="
  if ! helm lint "$chart"; then
    FAILED=1
  fi
  if ! helm template test "$chart" > /dev/null; then
    FAILED=1
  fi
done

exit $FAILED
