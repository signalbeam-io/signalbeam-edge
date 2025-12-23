# Local Development Guide

This guide will help you set up SignalBeam Edge for local development. We recommend using .NET Aspire for the best development experience.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Quick Start with .NET Aspire](#quick-start-with-net-aspire)
- [Alternative: Docker Compose](#alternative-docker-compose)
- [Development Workflow](#development-workflow)
- [Running Individual Services](#running-individual-services)
- [Database Operations](#database-operations)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### Required Software

1. **.NET 9 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/9.0))
   ```bash
   # Verify installation
   dotnet --version
   # Should show: 9.0.x
   ```

2. **.NET Aspire Workload**
   ```bash
   dotnet workload install aspire
   ```

3. **Docker Desktop** ([download](https://www.docker.com/products/docker-desktop))
   - Required for infrastructure services (PostgreSQL, NATS, Valkey, etc.)
   - Ensure Docker Desktop is running before starting development

4. **Node.js 20+** ([download](https://nodejs.org/))
   ```bash
   # Verify installation
   node --version
   # Should show: v20.x.x or higher
   ```

5. **Git** ([download](https://git-scm.com/downloads))

### Optional but Recommended

- **Visual Studio 2022** (v17.9+) or **Visual Studio Code** with C# Dev Kit
- **Azure CLI** - For Azure-related development
- **kubectl** - For Kubernetes development
- **Helm** - For Kubernetes package management

## Quick Start with .NET Aspire

.NET Aspire provides the best local development experience with:
- Unified dashboard for all services
- Automatic service discovery
- Built-in OpenTelemetry tracing
- One-command infrastructure setup

### Step 1: Clone the Repository

```bash
git clone https://github.com/signalbeam-io/signalbeam-edge.git
cd signalbeam-edge
```

### Step 2: Start the Backend Services

```bash
# Navigate to the Aspire AppHost
cd src/SignalBeam.AppHost

# Start all services (this will take a few minutes on first run)
dotnet run
```

This single command will:
- Start PostgreSQL with TimescaleDB
- Start Valkey (Redis-compatible cache)
- Start NATS with JetStream
- Start Azure Storage Emulator (Azurite)
- Launch all backend microservices:
  - DeviceManager (port 5001)
  - BundleOrchestrator (port 5002)
  - TelemetryProcessor (port 5003)
  - EdgeAgent (console app)

### Step 3: Access the Aspire Dashboard

The Aspire Dashboard will automatically open at: `http://localhost:15888`

**Dashboard Features:**
- **Resources Tab**: See all running services and their status
- **Console Logs**: View aggregated logs from all services
- **Traces**: Distributed tracing across all services
- **Metrics**: Real-time metrics visualization
- **Environment**: Inspect service configuration

### Step 4: Start the Frontend

In a **new terminal window**:

```bash
cd web
npm install
npm run dev
```

The web UI will be available at: `http://localhost:5173`

### Step 5: Verify Everything is Running

1. **Backend API (BundleOrchestrator)**: http://localhost:5002/scalar/v1
2. **Backend API (DeviceManager)**: http://localhost:5001/scalar/v1
3. **Frontend**: http://localhost:5173
4. **Aspire Dashboard**: http://localhost:15888

## Alternative: Docker Compose

If you prefer not to use .NET Aspire, you can use Docker Compose:

```bash
# Start infrastructure services
docker-compose up -d

# Start backend services manually
cd src/DeviceManager/SignalBeam.DeviceManager.Host
dotnet run

# In another terminal
cd src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host
dotnet run

# In another terminal
cd src/TelemetryProcessor/SignalBeam.TelemetryProcessor.Host
dotnet run

# Start frontend
cd web
npm install
npm run dev
```

## Development Workflow

### Making Code Changes

**Backend (.NET):**

1. Make your changes in the service code
2. .NET Aspire will automatically detect changes and restart the service
3. Watch the Aspire Dashboard for logs and errors
4. Test your changes using the Scalar API documentation (e.g., http://localhost:5002/scalar/v1)

**Frontend (React):**

1. Make your changes in the React code
2. Vite will automatically hot-reload your changes
3. Check the browser console for errors
4. Test your changes in the browser at http://localhost:5173

### Running Tests

**Backend Tests:**

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter Category!=Integration

# Run only integration tests (requires Docker)
dotnet test --filter Category=Integration

# Run tests with coverage
dotnet test /p:CollectCoverage=true
```

**Frontend Tests:**

```bash
cd web

# Run tests once
npm test

# Run tests in watch mode
npm test -- --watch

# Run tests with coverage
npm test -- --coverage
```

### Code Quality

**Format Code:**

```bash
# Format .NET code
dotnet format

# Format frontend code
cd web
npm run lint:fix
```

**Type Checking:**

```bash
cd web
npm run type-check
```

## Running Individual Services

Sometimes you may want to run just one service for debugging or development.

### Prerequisites for Individual Services

Ensure infrastructure is running:

```bash
# Using Aspire (recommended)
cd src/SignalBeam.AppHost
dotnet run

# OR using Docker Compose
docker-compose up -d postgres valkey nats azurite
```

### Run a Single Service

```bash
# Example: Run BundleOrchestrator
cd src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host
dotnet run

# The service will be available at http://localhost:5002
# API documentation at http://localhost:5002/scalar/v1
```

### Debug in Visual Studio

1. Open `src/SignalBeam.sln` in Visual Studio
2. Set the desired service as the startup project (right-click → Set as Startup Project)
3. Press F5 to debug
4. OR set multiple startup projects to debug multiple services simultaneously

### Debug in VS Code

1. Open the root folder in VS Code
2. Install the C# Dev Kit extension
3. Open `.vscode/launch.json` (create if it doesn't exist)
4. Select the service you want to debug from the Run and Debug panel
5. Press F5 to start debugging

Example `launch.json`:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "BundleOrchestrator",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/bin/Debug/net9.0/SignalBeam.BundleOrchestrator.Host.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host",
      "stopAtEntry": false,
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

## Database Operations

### Connect to PostgreSQL

**Using psql (command line):**

```bash
# If using Aspire (get connection string from dashboard)
psql "Host=localhost;Port=5432;Database=signalbeam;Username=postgres;Password=postgres"

# If using Docker Compose
docker exec -it signalbeam-postgres psql -U postgres -d signalbeam
```

**Using a GUI Tool:**

- **pgAdmin**: http://localhost:5050 (if running with Docker Compose)
- **DBeaver**: Connect using:
  - Host: localhost
  - Port: 5432
  - Database: signalbeam
  - Username: postgres
  - Password: postgres

### Database Migrations

**Create a New Migration:**

```bash
cd src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Infrastructure
dotnet ef migrations add AddNewFeature --startup-project ../SignalBeam.BundleOrchestrator.Host
```

**Apply Migrations:**

Migrations are automatically applied on startup. To manually apply:

```bash
cd src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host
dotnet ef database update --project ../SignalBeam.BundleOrchestrator.Infrastructure
```

**Rollback a Migration:**

```bash
dotnet ef database update PreviousMigrationName --project ../SignalBeam.BundleOrchestrator.Infrastructure
```

**Remove Last Migration (if not applied):**

```bash
dotnet ef migrations remove --project ../SignalBeam.BundleOrchestrator.Infrastructure
```

### Enable TimescaleDB Extension

```sql
-- Connect to the database and run:
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Verify it's installed
\dx timescaledb
```

### View Database Schema

```bash
# List all tables
\dt

# Describe a table
\d bundle

# List all schemas
\dn
```

## Working with Infrastructure Services

### PostgreSQL + TimescaleDB

**Connection String (local):**
```
Host=localhost;Port=5432;Database=signalbeam;Username=postgres;Password=postgres
```

**View Logs:**
```bash
# Aspire Dashboard → Resources → postgres → View Logs
# OR using Docker
docker logs signalbeam-postgres -f
```

### Valkey (Redis Cache)

**Connection String (local):**
```
localhost:6379
```

**Connect with Redis CLI:**
```bash
# Install redis-cli first
brew install redis  # macOS
# or
apt-get install redis-tools  # Linux

# Connect
redis-cli -h localhost -p 6379

# Test
SET test "Hello Valkey"
GET test
```

### NATS with JetStream

**Connection URL (local):**
```
nats://localhost:4222
```

**NATS CLI:**
```bash
# Install NATS CLI
brew install nats-io/nats-tools/nats  # macOS
# or download from https://github.com/nats-io/natscli

# Connect and subscribe to a subject
nats sub "signalbeam.devices.events.>"

# Publish a test message
nats pub "signalbeam.devices.events.test" "Hello NATS"

# View JetStream streams
nats stream ls
nats stream info DEVICE_EVENTS
```

### Azure Blob Storage (Azurite Emulator)

**Connection String (local):**
```
UseDevelopmentStorage=true
```

**Azure Storage Explorer:**
- Download: https://azure.microsoft.com/en-us/products/storage/storage-explorer/
- Connect to local emulator (port 10000)

## Environment Configuration

### User Secrets (Recommended for Local Development)

Never commit sensitive data to Git. Use User Secrets instead:

```bash
cd src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host

# Initialize user secrets
dotnet user-secrets init

# Set secrets
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=signalbeam;Username=postgres;Password=postgres"
dotnet user-secrets set "AzureBlobStorage:ConnectionString" "UseDevelopmentStorage=true"

# List all secrets
dotnet user-secrets list

# Remove a secret
dotnet user-secrets remove "ConnectionStrings:DefaultConnection"

# Clear all secrets
dotnet user-secrets clear
```

### appsettings.Development.json

Override settings for local development:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=signalbeam;Username=postgres;Password=postgres"
  },
  "AzureBlobStorage": {
    "ConnectionString": "UseDevelopmentStorage=true"
  },
  "NATS": {
    "Url": "nats://localhost:4222"
  },
  "Valkey": {
    "ConnectionString": "localhost:6379"
  }
}
```

## Frontend Development

### Environment Variables

Create `.env.local` in the `web` directory:

```bash
# Backend API URLs
VITE_API_BASE_URL=http://localhost:5002
VITE_DEVICE_MANAGER_URL=http://localhost:5001
VITE_TELEMETRY_PROCESSOR_URL=http://localhost:5003

# Feature flags
VITE_ENABLE_ROLLOUTS=true
VITE_ENABLE_DEVICE_GROUPS=true
```

### Hot Module Replacement (HMR)

Vite automatically reloads changes. If HMR breaks:

```bash
# Stop the dev server (Ctrl+C)
# Clear Vite cache
rm -rf web/node_modules/.vite

# Restart
npm run dev
```

### React DevTools

Install the React DevTools browser extension:
- [Chrome](https://chrome.google.com/webstore/detail/react-developer-tools/fmkadmapgofadopljbjfkapdkoienihi)
- [Firefox](https://addons.mozilla.org/en-US/firefox/addon/react-devtools/)

## Troubleshooting

### Aspire Dashboard Not Opening

**Problem:** Aspire dashboard doesn't open automatically

**Solution:**
```bash
# Check the console output for the dashboard URL
# It should show something like:
# Now listening on: http://localhost:15888

# Manually open http://localhost:15888 in your browser
```

### Port Already in Use

**Problem:** `Address already in use` error

**Solution:**
```bash
# Find process using the port (e.g., 5002)
# macOS/Linux
lsof -i :5002

# Windows
netstat -ano | findstr :5002

# Kill the process
# macOS/Linux
kill -9 <PID>

# Windows
taskkill /PID <PID> /F
```

### Database Connection Errors

**Problem:** `Npgsql.NpgsqlException: Connection refused`

**Solution:**
1. Ensure Docker Desktop is running
2. Check if PostgreSQL container is running:
   ```bash
   docker ps | grep postgres
   ```
3. Restart PostgreSQL:
   ```bash
   docker restart signalbeam-postgres
   ```
4. Check connection string in `appsettings.Development.json`

### Migration Errors

**Problem:** `The migration 'XXX' has already been applied to the database`

**Solution:**
```bash
# Reset the database (WARNING: deletes all data)
cd src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Infrastructure
dotnet ef database drop --startup-project ../SignalBeam.BundleOrchestrator.Host
dotnet ef database update --startup-project ../SignalBeam.BundleOrchestrator.Host
```

### Frontend Build Errors

**Problem:** TypeScript errors or dependency issues

**Solution:**
```bash
cd web

# Clear node_modules and reinstall
rm -rf node_modules package-lock.json
npm install

# Clear TypeScript cache
rm -rf tsconfig.tsbuildinfo

# Restart dev server
npm run dev
```

### CORS Errors

**Problem:** `Access to fetch at 'http://localhost:5002' from origin 'http://localhost:5173' has been blocked by CORS`

**Solution:**
CORS should be configured automatically in development mode. If you still see errors:

1. Check `Program.cs` for CORS configuration:
   ```csharp
   if (app.Environment.IsDevelopment())
   {
       app.UseCors("WebDev");
   }
   ```

2. Ensure the frontend URL (http://localhost:5173) is in the CORS policy

### Slow Performance

**Problem:** Services are slow or unresponsive

**Solution:**
1. Check Docker Desktop resources:
   - Docker Desktop → Settings → Resources
   - Increase CPU and Memory allocation
2. Close unnecessary applications
3. Clear Docker system:
   ```bash
   docker system prune -a
   ```

## Best Practices

1. **Always use .NET Aspire for local development** - it provides the best experience
2. **Keep User Secrets for sensitive data** - never commit passwords or keys
3. **Run tests before committing** - ensure your changes don't break existing functionality
4. **Use feature branches** - never commit directly to main
5. **Keep dependencies updated** - regularly update NuGet and npm packages
6. **Monitor the Aspire Dashboard** - watch for errors and performance issues
7. **Use Scalar API docs** - test API endpoints interactively before writing frontend code

## Next Steps

- [Contributing Guidelines](../../CONTRIBUTING.md) - How to contribute to the project
- [Code Style Guide](code-style.md) - Coding conventions and standards
- [Testing Strategy](../testing/testing-strategy.md) - How to write effective tests
- [API Documentation](../architecture/api-overview.md) - REST API reference

## Getting Help

- **Documentation**: Check the [docs/](../) directory
- **GitHub Issues**: [Report bugs or request features](https://github.com/signalbeam-io/signalbeam-edge/issues)
- **GitHub Discussions**: [Ask questions](https://github.com/signalbeam-io/signalbeam-edge/discussions)
