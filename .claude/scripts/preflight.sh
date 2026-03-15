#!/usr/bin/env bash
# Shared pre-flight check for /complete-task and /complete-infra
# Verifies branch, clean tree, and extracts issue number from branch name.
#
# Usage: source .claude/scripts/preflight.sh
# Outputs: BRANCH, ISSUE (or exits 1 on failure)

set -euo pipefail

BRANCH=$(git branch --show-current)

if [ "$BRANCH" = "main" ] || [ "$BRANCH" = "master" ]; then
  echo "ERROR: Cannot complete task on main branch"
  exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
  echo "ERROR: Working tree is dirty. Commit or stash changes first."
  exit 1
fi

ISSUE=$(echo "$BRANCH" | grep -oE '/[0-9]+' | tr -d '/' || true)

echo "Branch: $BRANCH"
echo "Issue:  ${ISSUE:-<none>}"
