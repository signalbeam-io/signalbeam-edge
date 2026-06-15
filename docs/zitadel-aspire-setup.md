# Running Zitadel with .NET Aspire

This guide explains how to run SignalBeam with Zitadel authentication using .NET Aspire for local development.

## Overview

.NET Aspire orchestrates all services including:
- **PostgreSQL** - Database for both SignalBeam and Zitadel
- **Zitadel** - OIDC authentication server
- **Valkey** - Distributed cache
- **NATS** - Message broker with JetStream
- **Azurite** - Azure Storage emulator
- **All Microservices** - DeviceManager, BundleOrchestrator, TelemetryProcessor, IdentityManager
- **API Gateway** - YARP reverse proxy (proxies OIDC requests to Zitadel)

## Architecture

```
Frontend (Vite:5173)
    ↓
API Gateway (localhost:8080)
    ↓
    ├─→ /api/auth/* → IdentityManager
    ├─→ /api/devices/* → DeviceManager
    ├─→ /api/bundles/* → BundleOrchestrator
    ├─→ /.well-known/* → Zitadel (localhost:9080)
    └─→ /oauth/* → Zitadel (localhost:9080)
```

**Key Points:**
- Frontend accesses Zitadel through API Gateway at `http://localhost:8080`
- Backend services validate JWT tokens directly against Zitadel at `http://localhost:9080`
- This setup simulates production where API Gateway proxies OIDC endpoints

## Prerequisites

1. **.NET 10 SDK** installed
2. **.NET Aspire Workload** installed:
   ```bash
   dotnet workload install aspire
   ```
3. **Docker Desktop** running (for containers)
4. **Node.js 20+** for frontend

## Step 1: Start Aspire

Start all backend services with Aspire:

```bash
cd src/SignalBeam.AppHost
dotnet run
```

**First Time:** You'll be prompted for the `postgres-password` parameter. Enter any password (e.g., `postgres`). This will be stored securely and reused for subsequent runs.

This will:
1. Pull and start all required containers (PostgreSQL, Zitadel, Valkey, NATS, Azurite)
2. Apply database migrations
3. Start all microservices
4. Open Aspire Dashboard at `http://localhost:15888`

**⏰ First Run:** Zitadel initialization takes 1-2 minutes. Wait for Zitadel to show "Running" in the Aspire dashboard before proceeding.

## Step 2: Access Aspire Dashboard

Open `http://localhost:15888` in your browser.

You'll see:
- **Resources** - All running services
- **Console Logs** - Aggregated logs from all services
- **Traces** - Distributed tracing
- **Metrics** - Real-time metrics

**Key Resources:**
- `zitadel` - Should show status "Running" on port 9080
- `postgres` - PostgreSQL database
- `api-gateway` - Running on port 8080
- `identity-manager` - IdentityManager service

## Step 3: Configure Zitadel

Access Zitadel Console directly:

```
http://localhost:9080
```

### First-Time Setup

1. **Login** with default admin credentials:
   - Username: `zitadel-admin@zitadel.localhost`
   - Password: `Password1!` (check Zitadel logs if different)

2. **Create Organization** (if not exists):
   - Go to: Organizations → Create New
   - Name: `SignalBeam`

3. **Create Application**:
   - Go to: Projects → Default Project → New Application
   - Name: `SignalBeam Web`
   - Type: **WEB**
   - Authentication Method: **PKCE**
   - Click "Continue"

4. **Configure Redirect URIs**:
   - Redirect URIs:
     ```
     http://localhost:5173/callback
     http://localhost:3000/callback
     ```
   - Post Logout Redirect URIs:
     ```
     http://localhost:5173
     http://localhost:3000
     ```
   - Origins (CORS):
     ```
     http://localhost:5173
     http://localhost:3000
     http://localhost:8080
     ```

5. **Grant Types** - Ensure these are enabled:
   - ✅ Authorization Code
   - ✅ Refresh Token

6. **Copy Client ID**:
   - After creating the app, copy the `Client ID` shown
   - Example: `123456789@signalbeam`

## Step 4: Configure Frontend

Update `web/.env.development`:

```env
# API Gateway (YARP) - routes to all microservices
VITE_API_URL=http://localhost:8080
VITE_APP_ENV=development
VITE_ENABLE_DEVTOOLS=true

# Authentication Mode
VITE_AUTH_MODE=zitadel

# Zitadel Configuration
# IMPORTANT: Use API Gateway URL (port 8080), NOT direct Zitadel (port 9080)
VITE_ZITADEL_AUTHORITY=http://localhost:8080
VITE_ZITADEL_CLIENT_ID=123456789@signalbeam  # Replace with your Client ID
VITE_ZITADEL_REDIRECT_URI=http://localhost:5173/callback
VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=http://localhost:5173
```

**⚠️ Important:** Always use `http://localhost:8080` (API Gateway) for `VITE_ZITADEL_AUTHORITY`, NOT `http://localhost:9080` (direct Zitadel). The API Gateway proxies OIDC requests to Zitadel.

## Step 5: Start Frontend

```bash
cd web
npm install
npm run dev
```

Frontend will start at `http://localhost:5173`

## Step 6: Test Authentication Flow

1. **Open Browser** → `http://localhost:5173`
2. **Click "Sign in with Zitadel"**
3. **Zitadel Login Page** → Create a new user or login
4. **First-time User:**
   - After successful Zitadel login, you'll be redirected to `/register`
   - Enter workspace name (e.g., "Acme Corp")
   - Workspace slug auto-generates (e.g., "acme-corp")
   - Click "Create Workspace"
5. **Dashboard** → You're now logged in!

## Verifying the Setup

### Check Zitadel is Running

```bash
curl http://localhost:9080/.well-known/openid-configuration
```

Should return Zitadel's OIDC configuration.

### Check API Gateway Proxy

```bash
curl http://localhost:8080/.well-known/openid-configuration
```

Should return the same OIDC configuration (proxied from Zitadel).

### Check IdentityManager

```bash
curl http://localhost:8080/api/auth/health
```

Should return `Healthy` or service status.

## Aspire Configuration Details

### PostgreSQL Password Parameter

From `src/SignalBeam.AppHost/Program.cs`:

```csharp
// PostgreSQL password parameter
var postgresPassword = builder.AddParameter("postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);
```

**Key Points:**
- Password is a parameter, not hardcoded
- Marked as `secret: true` for secure handling
- You'll be prompted once, then stored in user secrets
- Same password used for both SignalBeam and Zitadel databases

### Zitadel Container Configuration

From `src/SignalBeam.AppHost/Program.cs`:

```csharp
// Database resource name must differ from container name
var zitadelDb = postgres.AddDatabase("zitadel-db");

var zitadel = builder.AddContainer("zitadel", "ghcr.io/zitadel/zitadel", "v2.66.3")
    .WithArgs("start-from-init", "--masterkey", "MasterkeyNeedsToHave32Characters", "--tlsMode", "disabled")
    .WithHttpEndpoint(port: 9080, targetPort: 8080, name: "zitadel")
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_HOST", postgres.Resource.Name)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_DATABASE", zitadelDb.Resource.DatabaseName)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_USER_PASSWORD", postgresPassword)
    .WithEnvironment("ZITADEL_DATABASE_POSTGRES_ADMIN_PASSWORD", postgresPassword)
    .WithEnvironment("ZITADEL_EXTERNALDOMAIN", "localhost:8080")
    .WithEnvironment("ZITADEL_EXTERNALPORT", "8080")
    .WaitFor(postgres)
    .WithLifetime(ContainerLifetime.Persistent);
```

**Key Points:**
- Database resource named "zitadel-db" to avoid conflict with container name "zitadel"
- Actual PostgreSQL database name is referenced via `zitadelDb.Resource.DatabaseName`
- Zitadel runs on **port 9080** internally
- External domain is set to `localhost:8080` (API Gateway)
- TLS disabled for local development
- Uses same PostgreSQL password parameter
- Waits for PostgreSQL to be ready before starting
- Persistent lifetime means data survives restarts

### Service Discovery

All microservices automatically discover Zitadel through Aspire:

```csharp
var identityManager = builder.AddProject<Projects.SignalBeam_IdentityManager_Host>("identity-manager")
    .WithReference(signalbeamDb)
    .WithReference(valkey)
    .WithEnvironment("NATS__Url", nats.GetEndpoint("nats"))
    .WithEnvironment("Authentication__Jwt__Authority", zitadel.GetEndpoint("zitadel"))
    .WithEnvironment("Authentication__Jwt__Audience", "api://signalbeam-api")
    .WithEnvironment("Authentication__Jwt__RequireHttpsMetadata", "false");
```

**How it works:**
- `zitadel.GetEndpoint("zitadel")` resolves to `http://zitadel:8080` (internal Docker network)
- Aspire injects the correct Zitadel endpoint URL at runtime
- Services validate JWT tokens directly against Zitadel
- No hardcoded URLs needed!

### API Gateway Configuration

The API Gateway uses YARP to proxy OIDC requests to Zitadel:

```csharp
var apiGateway = builder.AddProject<Projects.SignalBeam_ApiGateway>("api-gateway")
    .WithHttpEndpoint(port: 8080, name: "gateway")
    .WithReference(deviceManager)
    .WithReference(bundleOrchestrator)
    .WithReference(telemetryProcessor)
    .WithReference(identityManager)
    .WithEnvironment("ReverseProxy__Clusters__zitadel__Destinations__destination1__Address",
        zitadel.GetEndpoint("zitadel"));
```

**Key Points:**
- YARP configuration in `appsettings.json` defines routes to Zitadel
- Aspire overrides the destination address with the actual container endpoint
- Frontend sees Zitadel at `http://localhost:8080` (through API Gateway)
- Backend services access Zitadel directly for JWT validation

## Troubleshooting

### Zitadel Not Starting

**Symptom:** Zitadel shows "Unhealthy" or keeps restarting

**Solutions:**
1. Check Aspire Dashboard logs for Zitadel container
2. Ensure PostgreSQL is running first (Zitadel depends on it)
3. Delete volume and restart:
   ```bash
   docker volume rm signalbeam_zitadel_data
   ```
4. Check if port 9080 is already in use:
   ```bash
   lsof -i :9080
   ```

### "Sign in with Zitadel" Button Disabled

**Cause:** Missing Zitadel configuration in `.env.development`

**Solution:** Ensure `VITE_ZITADEL_AUTHORITY` and `VITE_ZITADEL_CLIENT_ID` are set

### Redirect Loop After Login

**Cause:** Misconfigured redirect URIs in Zitadel

**Solution:** Verify redirect URIs in Zitadel application settings match exactly:
- `http://localhost:5173/callback` (not `https`, not different port)

### "User not found" After Successful Login

**Expected Behavior:** This is normal for new users!

Users are redirected to `/register` to create their workspace. After registration, they'll be directed to the dashboard.

### JWT Validation Fails

**Symptom:** IdentityManager logs show "Invalid token" or "Unauthorized"

**Solutions:**
1. Verify Zitadel authority in IdentityManager config matches Zitadel URL
2. Check audience matches: `api://signalbeam-api`
3. Ensure Zitadel is issuing tokens for the correct audience:
   - In Zitadel Console → Projects → Default Project
   - Add API resource with identifier: `api://signalbeam-api`

### Database Connection Issues

**Symptom:** Services fail to start with database connection errors

**Solution:**
1. Check PostgreSQL is running in Aspire Dashboard
2. Check connection string in service logs
3. Verify Aspire injected the correct connection string

## Data Persistence

Aspire containers use Docker volumes for persistence:

- **PostgreSQL data:** Persists between restarts
- **Zitadel configuration:** Persists between restarts
- **Valkey cache:** In-memory, cleared on restart

To reset everything:

```bash
# Stop Aspire (Ctrl+C)
# Remove all volumes
docker volume prune -f
# Restart Aspire
cd src/SignalBeam.AppHost
dotnet run
```

## Production Considerations

When moving to production:

1. **Use HTTPS** - Enable TLS on Zitadel and API Gateway
2. **Secure Masterkey** - Use a strong, randomly generated masterkey
3. **External Domain** - Set to your actual domain (e.g., `auth.signalbeam.io`)
4. **PostgreSQL** - Use managed PostgreSQL (Azure Database for PostgreSQL)
5. **Kubernetes** - Deploy Zitadel to AKS with Helm chart
6. **Secrets** - Store client secrets in Azure Key Vault

## Useful Commands

### View Zitadel Logs
```bash
# In Aspire Dashboard, click on "zitadel" → View Logs
# Or via Docker:
docker logs $(docker ps -q --filter ancestor=ghcr.io/zitadel/zitadel:v2.66.3)
```

### Restart a Single Service
```bash
# Stop Aspire
# Edit code
# Restart Aspire - hot reload will pick up changes
```

### Access PostgreSQL
```bash
# Get connection string from Aspire Dashboard
# Connect with psql or pgAdmin (running on Aspire-assigned port)
```

### View All Aspire Resources
```bash
# Aspire Dashboard automatically shows all resources
# Navigate to http://localhost:15888
```

## Next Steps

- [Complete Authentication Flow](../web/AUTHENTICATION.md)
- [Zitadel Production Setup](./ZITADEL_SETUP.md)
- [Configure Multi-Tenant Architecture](./ARCHITECTURE.md)

## Support

If you encounter issues:

1. Check Aspire Dashboard logs for errors
2. Verify all containers are "Running" status
3. Check Zitadel Console is accessible at `http://localhost:9080`
4. Review application configuration in Zitadel
5. Verify frontend `.env.development` settings
