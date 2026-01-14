# SignalBeam Edge - Quick Start Guide

Get up and running with SignalBeam in under 5 minutes with **fully automated** Zitadel setup!

## Prerequisites

- .NET 9 SDK
- Docker Desktop
- Node.js 20+
- jq (for config scripts): `brew install jq` (macOS) or `apt install jq` (Linux)

## 🚀 One-Command Startup

### Step 1: Start Backend with Aspire

```bash
cd src/SignalBeam.AppHost
dotnet run
```

This automatically:
- ✅ Starts PostgreSQL with TimescaleDB
- ✅ Starts Zitadel with admin user
- ✅ **Configures Zitadel via API** (creates project + application)
- ✅ Starts all microservices (DeviceManager, BundleOrchestrator, TelemetryProcessor, IdentityManager)
- ✅ Starts API Gateway
- ✅ Generates Zitadel config file at `/tmp/signalbeam-zitadel-config.json`

Wait for the Aspire dashboard to open: http://localhost:15888

### Step 2: Update Frontend Config

In a new terminal:

```bash
./scripts/update-frontend-config.sh
```

This reads the auto-generated Zitadel config and updates `web/.env.development`.

### Step 3: Start Frontend

```bash
cd web
npm install  # First time only
npm run dev
```

Frontend will be at: http://localhost:5173

## 🎉 You're Done!

Open http://localhost:5173 and:
1. Click "Register" to create a new account
2. Fill in the form (uses Zitadel for authentication)
3. You'll be redirected back to the dashboard

## 📋 Default Credentials

If you want to access Zitadel console directly:

- **URL**: http://localhost:9080/ui/console
- **Username**: `admin`
- **Password**: `Password1!`

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Aspire Dashboard                         │
│                  http://localhost:15888                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ├─► Zitadel (port 9080)
                              │   └─► Auto-configured via API
                              │
                              ├─► API Gateway (port 8080)
                              │   ├─► DeviceManager
                              │   ├─► BundleOrchestrator
                              │   ├─► TelemetryProcessor
                              │   └─► IdentityManager
                              │
                              ├─► PostgreSQL + TimescaleDB
                              ├─► NATS (JetStream)
                              └─► Valkey (Redis)

┌─────────────────────────────────────────────────────────────┐
│                     Frontend (Vite)                          │
│                  http://localhost:5173                       │
└─────────────────────────────────────────────────────────────┘
```

## 🔧 Troubleshooting

### Zitadel Setup Failed

**Symptom**: ZitadelSetup shows ❌ errors in Aspire dashboard

**Solution**:
```bash
# Check Zitadel logs in Aspire dashboard
# Ensure Zitadel container is running and healthy

# Manually run setup again
cd src/SignalBeam.ZitadelSetup
export ZITADEL_URL=http://localhost:9080
export ZITADEL_ADMIN_USER=admin
export ZITADEL_ADMIN_PASSWORD=Password1!
export CONFIG_OUTPUT_PATH=/tmp/signalbeam-zitadel-config.json
dotnet run
```

### Frontend Shows "Failed to fetch"

**Solution**:
```bash
# Verify API Gateway is running
curl http://localhost:8080/api/auth/me

# Check .env.development has correct values
cat web/.env.development

# Re-run config update script
./scripts/update-frontend-config.sh
```

### 401 Unauthorized Errors

**Solution**:
```bash
# Verify Client ID matches across all services
# Check: /tmp/signalbeam-zitadel-config.json
cat /tmp/signalbeam-zitadel-config.json

# Ensure backend services use the same Client ID
# Check Aspire dashboard environment variables for each service
```

## 🧹 Reset Everything

To start completely fresh:

```bash
# Stop Aspire (Ctrl+C)

# Clear Docker volumes
docker volume prune -f

# Clear generated config
rm /tmp/signalbeam-zitadel-config.json

# Restart Aspire
cd src/SignalBeam.AppHost
dotnet run
```

The setup will run again automatically and create new IDs.

## 📚 Next Steps

- **Read the docs**: [Zitadel Aspire Setup](./zitadel-aspire-setup.md)
- **Explore Aspire**: http://localhost:15888
- **View Zitadel config**: http://localhost:9080/ui/console
- **Try the API**: http://localhost:8080/swagger
- **Register devices**: See [Device Registration](./device-registration.md)

## 🛠️ Development Workflow

### Daily Development

1. Start backend: `cd src/SignalBeam.AppHost && dotnet run`
2. Start frontend: `cd web && npm run dev`
3. Code!

### Making Changes

- **Backend code**: Aspire auto-reloads on save
- **Frontend code**: Vite hot-reloads on save
- **Database schema**: Run migrations, Aspire will apply them
- **Zitadel config**: Setup tool is idempotent, just re-run Aspire

### Testing Authentication

1. Open http://localhost:5173
2. Click "Register"
3. Fill in email/password
4. Account is created in Zitadel
5. User record created in SignalBeam database
6. Tenant and subscription automatically created
7. You're logged in!

## 🎯 What's Automated vs Manual

### ✅ Fully Automated (Zero Manual Steps)

- Zitadel container startup
- Zitadel admin user creation
- Zitadel project creation via API
- Zitadel application (OIDC client) creation
- PostgreSQL database setup
- TimescaleDB extension installation
- Database migrations
- All microservices configuration

### ⚙️ One-Time Setup (Per Developer Machine)

- Install .NET 9 SDK
- Install Docker Desktop
- Install Node.js
- Run `npm install` in `web/` directory

### 📝 Manual Steps (Optional)

- Access Zitadel console (if you want to customize settings)
- Modify redirect URIs (if running on different ports)
- Add additional users via Zitadel UI (or use Register page)

## 🌟 Why This Setup is Better

**Before (Manual Setup)**:
1. Start Aspire
2. Open Zitadel console
3. Create project
4. Create application
5. Copy Client ID
6. Update backend config
7. Update frontend .env
8. Restart services
9. Hope it works

**After (Automated Setup)**:
1. `cd src/SignalBeam.AppHost && dotnet run`
2. `./scripts/update-frontend-config.sh`
3. `cd web && npm run dev`
4. ✨ It works!

## 📖 Additional Documentation

- [Architecture Overview](./architecture.md)
- [Automated Zitadel Setup Details](./zitadel-aspire-setup.md)
- [Authentication Flow](./web/authentication.md)
- [API Documentation](http://localhost:8080/scalar/v1) (when running)

## 🤝 Contributing

Found a bug? Have a feature request?
- [Create an issue](https://github.com/signalbeam-io/signalbeam-edge/issues)
- [Submit a PR](https://github.com/signalbeam-io/signalbeam-edge/pulls)

## 📜 License

[MIT License](./LICENSE)

---

**Happy coding!** 🚀

If you encounter any issues with the automated setup, please [open an issue](https://github.com/signalbeam-io/signalbeam-edge/issues/new) with the error logs from the Aspire dashboard.
