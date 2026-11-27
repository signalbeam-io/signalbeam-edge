# Simulator failure scenarios

The edge agent simulator (`src/EdgeAgent/SignalBeam.EdgeAgent.Simulator`) can inject failures into a
simulated fleet, so traces contain realistic error and edge-case flows next to the healthy ones.
Scenarios run in fleet mode (`--fleet-size 2` or more). The seed picks the affected devices, so the
same `--seed` and scenarios always hit the same devices; no device gets two scenarios.

## Running

From the AppHost (see [aspire-setup.md](aspire-setup.md)):

```bash
cd src/SignalBeam.AppHost
dotnet run -- --Simulator:Scenarios=offline,disk-fill --Simulator:ScenarioDevices=2 --Simulator:ScenarioStart=60
# or
dotnet run -- --Simulator:ScenarioFile=../../docs/examples/scenarios.json
```

Directly:

```bash
dotnet run --project src/EdgeAgent/SignalBeam.EdgeAgent.Simulator -- \
  --fleet-size 20 --seed 42 --nats-url nats://localhost:4222 \
  --scenario offline --scenario deploy-failure --scenario-devices 2 --scenario-start 120
```

| Option | Meaning |
|---|---|
| `--scenario <name>` | Scenario to inject; repeat it or comma-separate names. |
| `--scenario-devices N` | Devices per `--scenario` (default 1). |
| `--scenario-start S` | Seconds after the fleet starts sending (after setup) before failures begin (default 120). |
| `--scenario-duration S` | How long each `--scenario` lasts. Default: 10 minutes for spikes, until stopped for the rest. |
| `--scenario-file path` | JSON file, combined with any `--scenario` flags. |
| `--nats-url url` / `SIM_NATS_URL` | Publish heartbeats and metrics over NATS like the real agent. Needed for every alert below; the AppHost sets it. |

Scenario file:

```json
{
  "scenarios": [
    { "name": "disk-fill", "devices": 2, "startSeconds": 60 },
    { "name": "cpu-spike", "devices": 1, "startSeconds": 300, "durationSeconds": 600 }
  ]
}
```

The simulator prints which device got which scenario. Each tick span of an affected device carries
`signalbeam.sim.scenario=<name>` while the scenario is active, so injected failures can be told
apart from organic ones in the traces.

## Scenarios and what the server does

Timings use the default server settings: TelemetryProcessor checks for stale devices every minute
(threshold 2 min), computes health scores every 30 s and evaluates alert rules every minute.

| Scenario | Device behaviour | Server-side behaviour |
|---|---|---|
| `offline` | Stops all traffic (HTTP and NATS) from the start time until the simulator stops. | After ~2–3 min TelemetryProcessor publishes `signalbeam.devices.events.stale_detected`; DeviceManager marks the device **Offline** and logs a `DeviceOffline` activity. After 5 min a `DeviceOffline` alert (Warning; Critical after 30 min). |
| `flapping` | Alternates 4 min offline and 4 min online. | The device goes **Offline** in each offline phase and back **Online** with the first heartbeat after it. |
| `cpu-spike` | CPU at 96–99.5% (default for 10 min). | Health score's resource part drops; after 3 min of samples at ≥ 90% a `HighResourceUtilization` alert ("running out of CPU"). |
| `memory-spike` | Memory at 95–99.5% (default for 10 min). | Same as `cpu-spike`, for memory. |
| `disk-fill` | Disk grows from its normal level to ~99% over 5 min and stays full. | `HighResourceUtilization` alert for disk once the last 3 min are all ≥ 90% (about 7–8 min after start). |
| `deploy-failure` | Fails to roll out its desired bundle: reports reconciliation `failed` (with an image-pull error) to BundleOrchestrator and `Failed` bundle status to DeviceManager, and sends heartbeats with status `ReconciliationFailed`. | The device's rollout status becomes **Failed** with the error message; its `bundleDeploymentStatus` becomes `Failed`; a `HighErrorRate` alert after the 5-min window. Needs a bundle assigned to the device (below). |

Healthy devices with a desired bundle report `succeeded`, so their rollout status becomes
**Succeeded**.

### Assigning a bundle for `deploy-failure`

Fleet devices only reconcile once they have a desired state. With the dev tenant API key:

```bash
API=http://localhost:8080; KEY="X-Api-Key: dev-api-key-1"; TENANT=00000000-0000-0000-0000-000000000001
BUNDLE=$(curl -s -H "$KEY" -H 'Content-Type: application/json' $API/api/bundles \
  -d "{\"tenantId\":\"$TENANT\",\"name\":\"sim-app\",\"version\":\"1.0.0\",\"containers\":[{\"name\":\"web\",\"image\":\"nginx:1.27\"}]}" \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["bundleId"])')
curl -s -H "$KEY" -H 'Content-Type: application/json' $API/api/devices/<deviceId>/bundle \
  -d "{\"bundleId\":\"$BUNDLE\",\"version\":\"1.0.0\"}"
```

### Seeing the results

- Device status and bundle status: `GET /api/devices/<deviceId>` through the gateway.
- Activity log: `GET /api/devices/<deviceId>/activity-log`.
- Rollout status per device: `GET /api/rollouts/bundles/<bundleId>`.
- Alerts: `GET /api/alerts` through the gateway with the same `X-Api-Key` (the dev tenant's alerts),
  or `select type, severity, title from telemetry_processor.alerts`.
- Traces: `.tracebeam/otlp/traces.jsonl`. A NATS heartbeat continues the simulator's trace into
  TelemetryProcessor (`simulate heartbeat` → `signalbeam.devices publish` → `receive` → `process`).
  The stale-device event continues from TelemetryProcessor's check into DeviceManager
  (`process signalbeam.devices.events.stale_detected`).

## Alert tenants

Alerts belong to the device's tenant. TelemetryProcessor only knows device IDs, so it looks the
tenant up in DeviceManager (`GET /api/internal/devices/{id}/tenant`, cached). BundleOrchestrator's
device endpoints (desired-state, reconciliation-status) take the device's own key, which it verifies
with DeviceManager (`POST /api/internal/device-credentials/verify`, cached); a key for a different
device gets 403. Operators reading desired state with a JWT, or the dev tenant key, go through the
tenant lookup instead, and another tenant's device reads as 404.

That endpoint is for service-to-service calls only: the gateway doesn't route `/api/internal`, only
the gateway and Zitadel have public Container Apps ingress (both checked by
`InternalEndpointExposureTests`), and callers must send the shared `X-Internal-Service-Key`,
configured as `InternalServices:ApiKey` in all three services. The AppHost generates one per run;
in Azure it is the `internal-service-key` Key Vault secret. Without a matching key, lookups fail:
rules skip alerts with a warning, and BundleOrchestrator answers device requests (desired-state,
reconciliation-status) with 503.

Alerts don't resolve themselves. While a device has an active alert of a type, no new one of that
type is created for it, so resolve old alerts (or reset the database) before rerunning a scenario.
