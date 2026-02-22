#!/usr/bin/env bash
set -euo pipefail

# Pre-commit hook: lint and format changed files
# Called by Claude Code PreToolUse hook on git commit

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // ""')

# Only intercept git commit commands
if [[ "$COMMAND" != *"git commit"* ]]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-.}"
ERRORS=()

# Get staged files
STAGED_CS=$(git -C "$PROJECT_DIR" diff --cached --name-only --diff-filter=ACM -- '*.cs' 2>/dev/null || true)
STAGED_TS=$(git -C "$PROJECT_DIR" diff --cached --name-only --diff-filter=ACM -- '*.ts' '*.tsx' 2>/dev/null || true)
STAGED_TF=$(git -C "$PROJECT_DIR" diff --cached --name-only --diff-filter=ACM -- '*.tf' 2>/dev/null || true)
STAGED_HELM=$(git -C "$PROJECT_DIR" diff --cached --name-only --diff-filter=ACM -- 'deploy/charts/*' 2>/dev/null || true)

# 1. dotnet format on staged C# files
if [[ -n "$STAGED_CS" ]]; then
  if ! dotnet format "$PROJECT_DIR/src/SignalBeam.sln" --verify-no-changes --verbosity quiet 2>/dev/null; then
    # Auto-fix
    dotnet format "$PROJECT_DIR/src/SignalBeam.sln" --verbosity quiet 2>/dev/null || true
    # Re-stage fixed files
    echo "$STAGED_CS" | while IFS= read -r f; do
      [[ -f "$PROJECT_DIR/$f" ]] && git -C "$PROJECT_DIR" add "$f"
    done
    ERRORS+=("dotnet format: auto-fixed C# formatting issues and re-staged files")
  fi
fi

# 2. ESLint on staged TS/TSX files
if [[ -n "$STAGED_TS" ]]; then
  if ! (cd "$PROJECT_DIR/web" && npx eslint --quiet $STAGED_TS 2>/dev/null); then
    # Auto-fix
    (cd "$PROJECT_DIR/web" && npx eslint --fix $STAGED_TS 2>/dev/null) || true
    echo "$STAGED_TS" | while IFS= read -r f; do
      [[ -f "$PROJECT_DIR/$f" ]] && git -C "$PROJECT_DIR" add "$f"
    done
    ERRORS+=("eslint: auto-fixed TypeScript lint issues and re-staged files")
  fi
fi

# 3. terraform fmt on staged .tf files
if [[ -n "$STAGED_TF" ]]; then
  if ! terraform fmt -check -recursive "$PROJECT_DIR/infra/" >/dev/null 2>&1; then
    terraform fmt -recursive "$PROJECT_DIR/infra/" >/dev/null 2>&1 || true
    echo "$STAGED_TF" | while IFS= read -r f; do
      [[ -f "$PROJECT_DIR/$f" ]] && git -C "$PROJECT_DIR" add "$f"
    done
    ERRORS+=("terraform fmt: auto-fixed Terraform formatting and re-staged files")
  fi
fi

# 4. helm lint on staged chart changes
if [[ -n "$STAGED_HELM" ]]; then
  CHART_DIRS=$(echo "$STAGED_HELM" | grep -oP 'deploy/charts/[^/]+' | sort -u)
  for chart_dir in $CHART_DIRS; do
    if [[ -f "$PROJECT_DIR/$chart_dir/Chart.yaml" ]]; then
      if ! helm lint "$PROJECT_DIR/$chart_dir" --quiet 2>/dev/null; then
        ERRORS+=("helm lint: $chart_dir has lint errors (cannot auto-fix)")
      fi
    fi
  done
fi

# Report results
if [[ ${#ERRORS[@]} -gt 0 ]]; then
  # Join errors
  MSG=$(printf '%s\n' "${ERRORS[@]}")

  # Check if any are non-auto-fixable
  if echo "$MSG" | grep -q "cannot auto-fix"; then
    echo "$MSG" >&2
    exit 2  # Block commit
  fi

  # All were auto-fixed — allow commit to proceed
  echo "$MSG" >&2
  exit 0
fi

exit 0
