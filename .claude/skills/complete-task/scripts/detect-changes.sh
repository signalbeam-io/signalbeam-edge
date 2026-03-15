#!/usr/bin/env bash
# Detect which areas of the codebase changed relative to origin/main.
# Sets flags used to gate downstream phases in /complete-task.
#
# Usage: bash .claude/skills/complete-task/scripts/detect-changes.sh
# Output: prints each flag as KEY=VALUE (parseable) and a human summary.

set -euo pipefail

CHANGED_FILES=$(git diff --name-only origin/main...HEAD)

HAS_BACKEND=$(echo "$CHANGED_FILES" | grep -c "^src/" || true)
HAS_FRONTEND=$(echo "$CHANGED_FILES" | grep -c "^web/" || true)
HAS_INFRA=$(echo "$CHANGED_FILES" | grep -cE "^(infra/|deploy/|\.github/workflows/)" || true)
HAS_ENDPOINTS=$(echo "$CHANGED_FILES" | grep -c "Endpoints/" || true)
HAS_ENTITIES=$(echo "$CHANGED_FILES" | grep -c "Domain/Entities/" || true)
HAS_EVENTS=$(echo "$CHANGED_FILES" | grep -c "Domain/Events/" || true)

echo "HAS_BACKEND=$HAS_BACKEND"
echo "HAS_FRONTEND=$HAS_FRONTEND"
echo "HAS_INFRA=$HAS_INFRA"
echo "HAS_ENDPOINTS=$HAS_ENDPOINTS"
echo "HAS_ENTITIES=$HAS_ENTITIES"
echo "HAS_EVENTS=$HAS_EVENTS"

echo ""
echo "Change detection:"
echo "  Backend:   $([ "$HAS_BACKEND" -gt 0 ] && echo YES || echo NO)"
echo "  Frontend:  $([ "$HAS_FRONTEND" -gt 0 ] && echo YES || echo NO)"
echo "  Infra:     $([ "$HAS_INFRA" -gt 0 ] && echo YES || echo NO)"
echo "  Endpoints: $([ "$HAS_ENDPOINTS" -gt 0 ] && echo YES || echo NO)"
echo "  Entities:  $([ "$HAS_ENTITIES" -gt 0 ] && echo YES || echo NO)"
echo "  Events:    $([ "$HAS_EVENTS" -gt 0 ] && echo YES || echo NO)"
