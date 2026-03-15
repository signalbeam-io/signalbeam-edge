#!/usr/bin/env bash
# Check all microservices for pending EF Core model changes.
# Exits 0 with output indicating which services have pending migrations.
#
# Usage: bash .claude/skills/complete-task/scripts/check-migrations.sh

set -euo pipefail

SERVICES="DeviceManager BundleOrchestrator TelemetryProcessor IdentityManager"
HAS_PENDING=0

for service in $SERVICES; do
  infra=$(find src -path "*/$service*Infrastructure*.csproj" | head -1)
  host=$(find src -path "*/$service*Host*.csproj" | head -1)

  if [ -n "$infra" ] && [ -n "$host" ]; then
    echo "Checking $service for pending migrations..."
    if ! dotnet ef migrations has-pending-model-changes --project "$infra" --startup-project "$host" 2>&1; then
      echo "WARNING: $service has pending model changes"
      HAS_PENDING=1
    fi
  fi
done

if [ "$HAS_PENDING" -gt 0 ]; then
  echo ""
  echo "PENDING_MIGRATIONS=true"
  echo "One or more services have pending migrations. Run /add-migration before proceeding."
else
  echo ""
  echo "PENDING_MIGRATIONS=false"
fi
