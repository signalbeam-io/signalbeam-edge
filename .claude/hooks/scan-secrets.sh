#!/usr/bin/env bash
set -euo pipefail

# Pre-commit/pre-push hook: scan staged files for leaked secrets
# Blocks the commit/push if potential secrets are found

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // ""')

# Only intercept git commit and git push commands
if [[ "$COMMAND" != *"git commit"* && "$COMMAND" != *"git push"* ]]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-.}"
ISSUES=()

# Get files to scan: staged for commit, branch diff for push
if [[ "$COMMAND" == *"git commit"* ]]; then
  FILES=$(git -C "$PROJECT_DIR" diff --cached --name-only --diff-filter=ACM 2>/dev/null || true)
elif [[ "$COMMAND" == *"git push"* ]]; then
  FILES=$(git -C "$PROJECT_DIR" diff origin/main...HEAD --name-only 2>/dev/null || true)
fi

if [[ -z "$FILES" ]]; then
  exit 0
fi

# Skip binary files and known safe paths
SCAN_FILES=""
while IFS= read -r f; do
  # Skip binary, lock files, and test fixtures
  case "$f" in
    *.png|*.jpg|*.jpeg|*.gif|*.ico|*.woff|*.woff2|*.ttf|*.eot|*.svg) continue ;;
    **/bin/**|**/obj/**|**/node_modules/**) continue ;;
    package-lock.json|*.lock) continue ;;
  esac
  if [[ -f "$PROJECT_DIR/$f" ]]; then
    SCAN_FILES="$SCAN_FILES $PROJECT_DIR/$f"
  fi
done <<< "$FILES"

if [[ -z "$SCAN_FILES" ]]; then
  exit 0
fi

# --- Secret patterns ---

# 1. AWS keys
if grep -rlP 'AKIA[0-9A-Z]{16}' $SCAN_FILES 2>/dev/null; then
  ISSUES+=("AWS Access Key ID found")
fi

# 2. Azure / Entra secrets (client secrets, SAS tokens)
if grep -rlP '(?i)(client.?secret|azure.?secret)\s*[:=]\s*["\x27][^\s"'\'']{8,}' $SCAN_FILES 2>/dev/null; then
  ISSUES+=("Azure/Entra client secret found")
fi
if grep -rlP 'sig=[A-Za-z0-9%+/=]{20,}' $SCAN_FILES 2>/dev/null; then
  ISSUES+=("Azure SAS token signature found")
fi

# 3. Generic private keys
if grep -rlP '\-\-\-\-\-BEGIN (RSA |EC |DSA |OPENSSH )?PRIVATE KEY\-\-\-\-\-' $SCAN_FILES 2>/dev/null; then
  ISSUES+=("Private key found in source")
fi

# 4. Connection strings with passwords
if grep -rlP '(?i)(password|pwd)\s*=' $SCAN_FILES 2>/dev/null | grep -v 'appsettings.Development.json' | grep -v '\.md$' | grep -v 'SKILL.md' | head -1 >/dev/null 2>&1; then
  # Check it's not a placeholder
  MATCHES=$(grep -rlP '(?i)(password|pwd)\s*=' $SCAN_FILES 2>/dev/null | grep -v 'appsettings.Development.json' | grep -v '\.md$' | grep -v 'SKILL.md' || true)
  if [[ -n "$MATCHES" ]]; then
    # Exclude known safe patterns (empty password, placeholder)
    REAL_MATCHES=$(grep -lP '(?i)(password|pwd)\s*=\s*["\x27](?![\s"\x27]|your-|placeholder|changeme|\{)' $SCAN_FILES 2>/dev/null | grep -v '\.md$' || true)
    if [[ -n "$REAL_MATCHES" ]]; then
      ISSUES+=("Connection string with password found in: $(echo "$REAL_MATCHES" | head -3 | tr '\n' ', ')")
    fi
  fi
fi

# 5. JWT tokens (eyJ... pattern)
if grep -rlP 'eyJ[A-Za-z0-9_-]{20,}\.eyJ[A-Za-z0-9_-]{20,}\.' $SCAN_FILES 2>/dev/null; then
  ISSUES+=("JWT token found in source")
fi

# 6. SignalBeam-specific: device API keys and registration tokens in source
if grep -rlP 'sb_device_[a-z0-9]{8}_[A-Za-z0-9]{20,}' $SCAN_FILES 2>/dev/null | grep -v '\.md$' | grep -v 'test' | grep -vi 'Test' | head -1 >/dev/null 2>&1; then
  ISSUES+=("Device API key (sb_device_*) found in non-test source")
fi
if grep -rlP 'sbt_[a-z0-9]{8}_[A-Za-z0-9]{20,}' $SCAN_FILES 2>/dev/null | grep -v '\.md$' | grep -v 'test' | grep -vi 'Test' | head -1 >/dev/null 2>&1; then
  ISSUES+=("Registration token (sbt_*) found in non-test source")
fi

# 7. GitHub tokens
if grep -rlP '(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{36,}' $SCAN_FILES 2>/dev/null; then
  ISSUES+=("GitHub token found")
fi

# 8. Generic high-entropy secrets (API_KEY=, SECRET=, TOKEN= with long values)
if grep -rlP '(?i)(api[_-]?key|secret[_-]?key|auth[_-]?token|access[_-]?token)\s*[:=]\s*["\x27][A-Za-z0-9+/=_-]{32,}' $SCAN_FILES 2>/dev/null | grep -v '\.md$' | grep -v 'SKILL.md' | head -1 >/dev/null 2>&1; then
  ISSUES+=("Potential hardcoded secret (long API key/token value) found")
fi

# 9. .env files being committed
while IFS= read -r f; do
  case "$f" in
    *.env|*.env.local|*.env.production|*.env.staging)
      ISSUES+=(".env file staged for commit: $f")
      ;;
  esac
done <<< "$FILES"

# --- Report ---

if [[ ${#ISSUES[@]} -gt 0 ]]; then
  MSG="SECRETS SCAN FAILED — potential secrets detected in staged files:\n"
  for issue in "${ISSUES[@]}"; do
    MSG="$MSG\n  - $issue"
  done
  MSG="$MSG\n\nReview the flagged files. If these are false positives (test fixtures, documentation), proceed with caution."
  echo -e "$MSG" >&2
  exit 2  # Block the commit/push
fi

exit 0
