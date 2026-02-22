#!/usr/bin/env bash
set -euo pipefail

# Post-edit hook: auto-format the file that was just edited/written
# Runs after Edit or Write tool completes

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // .tool_input.file_path // ""')

if [[ -z "$FILE_PATH" || ! -f "$FILE_PATH" ]]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-.}"

case "$FILE_PATH" in
  *.cs)
    # Format single C# file via dotnet format
    dotnet format "$PROJECT_DIR/src/SignalBeam.sln" --include "$FILE_PATH" --verbosity quiet 2>/dev/null || true
    ;;
  *.ts|*.tsx)
    # Format single TS/TSX file via ESLint fix
    (cd "$PROJECT_DIR/web" && npx eslint --fix "$FILE_PATH" 2>/dev/null) || true
    ;;
  *.tf)
    # Format single Terraform file
    terraform fmt "$FILE_PATH" 2>/dev/null || true
    ;;
esac

exit 0
