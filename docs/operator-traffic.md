# Operator traffic generator

`src/SignalBeam.TrafficGenerator` plays a scripted operator: it calls the control-plane APIs the web
UI uses, through the API gateway with the dev tenant API key, so traces cover operator flows and not
only device traffic. The AppHost starts it next to the fleet simulator (see [aspire-setup.md](aspire-setup.md)).

## What it does

A weighted mix of actions, mostly reads:

| Area | Actions |
|---|---|
| Devices | list (paged), filter by status, tag or group, detail, health, activity log |
| Groups and tags | list groups, group memberships, list tags, search devices by tag query, add and remove the `operator=checked` tag |
| Bundles | list, detail, create (`op-app-<run>-<n>`), new version, assign to a device |
| Phased rollouts | list, active, create for a device group, start, pause, resume, roll back |
| Alerts | list (optionally by status), statistics, acknowledge, resolve |
| Invalid requests (5% by default) | unknown device, bundle, alert or rollout (404), bundle without a name (400) |

Each action is one span, `operator <action>` with `signalbeam.operator.action`, wrapping the HTTP
calls it makes. Actions only target what the operator has already seen, in a state where the change
is valid, so the 4xx come from the invalid-request share.

What it changes:
- Bundles and rollouts: only its own (`op-app-*`, `op-rollout-*`). It assigns its own bundles to
  any fleet device.
- Tags: adds and removes `operator=checked` on any device.
- Alerts: acknowledges and resolves any of the tenant's alerts, as an operator would.

The same `--seed` gives the same action sequence and inputs. Created names include a run ID
(the start time), so a rerun doesn't collide with an earlier run's bundles.

## Running

From the AppHost (on by default):

```bash
cd src/SignalBeam.AppHost
dotnet run -- --Operator:Rate=60 --Operator:Seed=7   # actions/min, seed (defaults: 30, the simulator seed)
dotnet run -- --Operator:Enabled=false               # fleet only
```

It starts 60 s after the AppHost (`--Operator:StartDelay`), once the fleet has registered. The AppHost
raises DeviceManager's per-tenant rate limit to 300/min locally, because the fleet and the operator
share the dev tenant.

Directly, against a running stack:

```bash
dotnet run --project src/SignalBeam.TrafficGenerator -- \
  --gateway-url http://localhost:8080 --api-key dev-api-key-1 --seed 42 --rate 60 --iterations 200
```

| Option | Meaning |
|---|---|
| `--gateway-url` / `TRAFFIC_GATEWAY_URL` | Gateway base URL (default `http://localhost:8080`) |
| `--api-key` / `TRAFFIC_API_KEY` | Tenant API key, sent as `X-API-Key` (default `dev-api-key-1`) |
| `--tenant-id` / `TRAFFIC_TENANT_ID` | Tenant ID (default the dev tenant) |
| `--seed` | Seed for the action mix (default 42) |
| `--rate` | Actions per minute (default 30) |
| `--invalid-rate` | Share of invalid requests, 0 to 1 (default 0.05) |
| `--iterations` | Number of actions; 0 runs until stopped (default) |
| `--start-delay` | Seconds before the first action (default 0) |

## Known server errors

Creating a phased rollout for a device group currently returns 500 (#491), so `create-rollout`
spans show errors until that is fixed.
