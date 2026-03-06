---
name: run-local
description: Start the local development environment using .NET Aspire AppHost. Use to spin up all services locally for manual testing — runs PostgreSQL, NATS, Valkey, and all microservices via Aspire.
allowed-tools: Bash
user-invocable: true
---

# Run Local

Start the local development environment using .NET Aspire.

## Arguments

- `--backend-only` — Skip the frontend dev server
- `--no-dashboard` — Skip opening the Aspire dashboard

## Process

### Step 1: Pre-flight

```bash
# Verify Docker is running (required for PostgreSQL, NATS, Valkey, Zitadel)
docker info > /dev/null 2>&1 || echo "ERROR: Docker is not running. Start Docker first."

# Verify .NET SDK
dotnet --version
```

### Step 2: Start Aspire AppHost

```bash
cd src/SignalBeam.AppHost && dotnet run
```

The Aspire dashboard will be available at `https://localhost:15888`.

### Step 3: Start Frontend (unless --backend-only)

In a separate terminal:

```bash
cd web && npm run dev
```

Frontend available at `http://localhost:5173`.

## Ports

| Service | Port |
|---------|------|
| Aspire Dashboard | 15888 |
| API Gateway | 5000 |
| Frontend (Vite) | 5173 |
| PostgreSQL | 5432 |
| NATS | 4222 |
| Valkey (Redis) | 6379 |
| Zitadel | 8080 |

## Troubleshooting

- **Port conflict**: Check for running containers with `docker ps`
- **Build failure**: Run `dotnet build src/SignalBeam.sln` first to see errors
- **Docker containers stuck**: Run `docker compose down` in `src/SignalBeam.AppHost/` to clean up

## Related Skills

- `/run-tests` to run tests after verifying locally
- `/diagnose` if the local environment has issues
