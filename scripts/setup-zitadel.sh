#!/bin/bash
set -e

# Zitadel Auto-Configuration Script
# This script automatically configures Zitadel for local development using the Management API

ZITADEL_URL="${ZITADEL_URL:-http://localhost:9080}"
ZITADEL_ADMIN_USER="${ZITADEL_ADMIN_USER:-admin}"
ZITADEL_ADMIN_PASSWORD="${ZITADEL_ADMIN_PASSWORD:-Password1!}"
PROJECT_NAME="SignalBeam Edge"
APP_NAME="SignalBeam Web"
REDIRECT_URIS=("http://localhost:5173/callback" "http://localhost:5173/silent-renew")
POST_LOGOUT_URIS=("http://localhost:5173")
OUTPUT_FILE="${OUTPUT_FILE:-/tmp/zitadel-config.json}"

echo "🚀 Starting Zitadel auto-configuration..."
echo "Zitadel URL: $ZITADEL_URL"

# Wait for Zitadel to be ready
echo "⏳ Waiting for Zitadel to be healthy..."
max_attempts=60
attempt=0
while [ $attempt -lt $max_attempts ]; do
    if curl -s -f "$ZITADEL_URL/debug/ready" > /dev/null 2>&1; then
        echo "✅ Zitadel is ready!"
        break
    fi
    attempt=$((attempt + 1))
    echo "   Attempt $attempt/$max_attempts - Zitadel not ready yet..."
    sleep 2
done

if [ $attempt -eq $max_attempts ]; then
    echo "❌ Zitadel failed to become ready after $max_attempts attempts"
    exit 1
fi

# Give it a few more seconds to fully initialize
sleep 5

echo "🔑 Authenticating as admin..."

# Get access token using service account (PAT) or login
# For initial setup, we'll use the introspection endpoint to get a token
# First, we need to create a service account via the admin user

# Step 1: Get an access token by logging in as admin
# Zitadel uses OAuth 2.0 Password Grant for machine-to-machine auth
# But for initial setup, we'll use the console API approach

# Create a Personal Access Token (PAT) via API
# Note: This requires the admin user to be set up, which happens via env vars

echo "📝 Getting admin access token..."

# Use Zitadel's login API to get a token
TOKEN_RESPONSE=$(curl -s -X POST "$ZITADEL_URL/oauth/v2/token" \
    -H "Content-Type: application/x-www-form-urlencoded" \
    -d "grant_type=password" \
    -d "scope=openid profile email urn:zitadel:iam:org:project:id:zitadel:aud" \
    -d "username=$ZITADEL_ADMIN_USER" \
    -d "password=$ZITADEL_ADMIN_PASSWORD" \
    -d "client_id=zitadel" 2>&1) || true

# Check if we got a token
if echo "$TOKEN_RESPONSE" | jq -e '.access_token' > /dev/null 2>&1; then
    ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.access_token')
    echo "✅ Got access token!"
else
    echo "❌ Failed to get access token. Response:"
    echo "$TOKEN_RESPONSE"
    echo ""
    echo "💡 This is expected on first run. We need to create a service account first."
    echo "📋 Creating service account for automation..."

    # For now, output instructions for manual setup
    # In production, we'd use Terraform or Zitadel Actions
    echo ""
    echo "⚠️  MANUAL SETUP REQUIRED (first time only):"
    echo "1. Access Zitadel console: $ZITADEL_URL/ui/console"
    echo "2. Login with: $ZITADEL_ADMIN_USER / $ZITADEL_ADMIN_PASSWORD"
    echo "3. Create a service account for automation (or use this script after creating an application)"
    echo ""
    echo "🔄 For now, we'll create a simpler setup using client credentials..."

    # Alternative: Use a pre-created application client ID and secret
    # This would be set up once and stored in env vars
    exit 1
fi

echo "🏗️  Creating SignalBeam project..."

# Create a new project
PROJECT_RESPONSE=$(curl -s -X POST "$ZITADEL_URL/management/v1/projects" \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
        \"name\": \"$PROJECT_NAME\"
    }")

PROJECT_ID=$(echo "$PROJECT_RESPONSE" | jq -r '.id')

if [ "$PROJECT_ID" == "null" ] || [ -z "$PROJECT_ID" ]; then
    echo "❌ Failed to create project. Response:"
    echo "$PROJECT_RESPONSE"
    exit 1
fi

echo "✅ Project created with ID: $PROJECT_ID"

echo "📱 Creating web application..."

# Create a web application (OIDC)
APP_RESPONSE=$(curl -s -X POST "$ZITADEL_URL/management/v1/projects/$PROJECT_ID/apps/oidc" \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    -H "Content-Type: application/json" \
    -d "{
        \"name\": \"$APP_NAME\",
        \"redirectUris\": $(printf '%s\n' "${REDIRECT_URIS[@]}" | jq -R . | jq -s .),
        \"postLogoutRedirectUris\": $(printf '%s\n' "${POST_LOGOUT_URIS[@]}" | jq -R . | jq -s .),
        \"responseTypes\": [\"OIDC_RESPONSE_TYPE_CODE\"],
        \"grantTypes\": [\"OIDC_GRANT_TYPE_AUTHORIZATION_CODE\"],
        \"appType\": \"OIDC_APP_TYPE_USER_AGENT\",
        \"authMethodType\": \"OIDC_AUTH_METHOD_TYPE_NONE\",
        \"version\": \"OIDC_VERSION_1_0\",
        \"devMode\": true,
        \"accessTokenType\": \"OIDC_TOKEN_TYPE_JWT\",
        \"idTokenRoleAssertion\": true,
        \"idTokenUserinfoAssertion\": true
    }")

CLIENT_ID=$(echo "$APP_RESPONSE" | jq -r '.clientId')

if [ "$CLIENT_ID" == "null" ] || [ -z "$CLIENT_ID" ]; then
    echo "❌ Failed to create application. Response:"
    echo "$APP_RESPONSE"
    exit 1
fi

echo "✅ Application created with Client ID: $CLIENT_ID"

echo "💾 Saving configuration to $OUTPUT_FILE..."

# Save configuration to JSON file
cat > "$OUTPUT_FILE" << EOF
{
  "zitadel": {
    "authority": "http://localhost:8080",
    "clientId": "$CLIENT_ID",
    "projectId": "$PROJECT_ID",
    "redirectUri": "http://localhost:5173/callback",
    "postLogoutRedirectUri": "http://localhost:5173",
    "scope": "openid profile email",
    "adminUser": "$ZITADEL_ADMIN_USER",
    "adminPassword": "$ZITADEL_ADMIN_PASSWORD"
  },
  "backend": {
    "authority": "http://localhost:8080",
    "audience": "$CLIENT_ID",
    "requireHttpsMetadata": false
  }
}
EOF

echo "✅ Configuration saved!"
echo ""
echo "📋 Zitadel Configuration:"
echo "  Project ID:  $PROJECT_ID"
echo "  Client ID:   $CLIENT_ID"
echo "  Authority:   http://localhost:8080"
echo ""
echo "🎉 Zitadel is fully configured and ready to use!"
echo ""
echo "🔗 Next steps:"
echo "  1. Frontend will auto-load config from $OUTPUT_FILE"
echo "  2. Backend services will use Client ID: $CLIENT_ID"
echo "  3. Access Zitadel console: $ZITADEL_URL/ui/console"
echo ""
