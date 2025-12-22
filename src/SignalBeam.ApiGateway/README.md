# SignalBeam API Gateway

YARP-based reverse proxy that provides a single entry point for all SignalBeam microservices.

## Purpose

Routes requests from the frontend to the appropriate microservice:
- `/api/devices/*` → DeviceManager (port 5296)
- `/api/groups/*` → DeviceManager (port 5296)
- `/api/bundles/*` → BundleOrchestrator (port 5002)
- `/api/rollouts/*` → BundleOrchestrator (port 5002)
- `/api/heartbeat/*` → TelemetryProcessor (port 5003)

## Running

```bash
cd src/SignalBeam.ApiGateway
dotnet run
```

Gateway will start on `http://localhost:8080`

## Configuration

Routing configuration is in `appsettings.json` under the `ReverseProxy` section.

To add a new route:
1. Add a route in `Routes` section
2. Add the backend service in `Clusters` section

## CORS

Configured to allow requests from:
- `http://localhost:5173` (Vite dev server)
- `http://localhost:3000`
- `http://localhost:4173`

Update `Program.cs` to add more origins if needed.
