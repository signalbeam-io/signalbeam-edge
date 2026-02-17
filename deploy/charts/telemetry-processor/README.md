# TelemetryProcessor Helm Chart

Deploys the SignalBeam TelemetryProcessor service — metrics ingestion, heartbeat processing, device health monitoring, and alerting via NATS.

## Quick Start

```bash
helm install telemetry-processor deploy/charts/telemetry-processor -n signalbeam --create-namespace

# With environment-specific values
helm install telemetry-processor deploy/charts/telemetry-processor -n signalbeam \
  -f deploy/charts/telemetry-processor/values-dev.yaml
```

## Environment Values

| File | Replicas | HPA | Scrape Interval |
|------|----------|-----|-----------------|
| `values.yaml` | 3 | 3-15 | 15s |
| `values-dev.yaml` | 1 | off | disabled |
| `values-staging.yaml` | 3 | 3-8 | 15s |
| `values-prod.yaml` | 3 | 3-15 | 10s |

## Prerequisites

```bash
kubectl create secret generic telemetry-processor-db -n signalbeam \
  --from-literal=connection-string="Host=postgres;Database=signalbeam_telemetry;Username=app;Password=secret"

kubectl create secret generic telemetry-processor-nats -n signalbeam \
  --from-literal=token="nats-auth-token"
```

## NATS Configuration

The TelemetryProcessor subscribes to NATS subjects for heartbeats and metrics. Configure the NATS URL via `config.NATS__Url` and authentication token via the `telemetry-processor-nats` secret.

## Scaling

This service is designed for high throughput. Default HPA scales 3-15 replicas based on CPU (65-70%) and memory (75-80%). Production uses higher resource limits (2 CPU, 1Gi) to handle metric ingestion spikes.

## Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| `config.NATS__Url` | `nats://nats.signalbeam:4222` | NATS server URL |
| `config.DeviceStatusMonitor__CheckIntervalSeconds` | `30` | Status check interval |
| `config.DeviceStatusMonitor__OfflineThresholdSeconds` | `120` | Offline threshold |
| `config.MetricsAggregation__IntervalSeconds` | `60` | Aggregation interval |
| `config.HealthMonitor__CheckIntervalSeconds` | `30` | Health check interval |
| `serviceMonitor.interval` | `15s` | Prometheus scrape interval |
