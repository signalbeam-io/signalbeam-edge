# Monitoring

Observability differs by deployment path. AKS runs a self-hosted stack; ACA uses
Azure Log Analytics.

## Signals

Every service emits the same three signals via OpenTelemetry (configured in
`SignalBeam.ServiceDefaults`):

- **Metrics** — RED metrics (rate, errors, duration) plus business counters
  (devices registered, rollouts completed) at `/metrics`.
- **Logs** — structured Serilog with `DeviceId`/`TenantId` fields.
- **Traces** — distributed traces across the gateway and services.

Health endpoints: `/health/live` (process up, no dependency checks) and
`/health/ready` (dependencies reachable).

## AKS path

Telemetry flows through the OTEL Collector to backends, visualized in Grafana.

```
Services → OTEL Collector → Tempo (traces)
                          → Prometheus (metrics)
                          → Loki (logs)
                          → Grafana (dashboards)
```

| Component | Namespace | Service |
|-----------|-----------|---------|
| OTEL Collector | `signalbeam` | `otel-collector.signalbeam:4317` |
| Prometheus | `monitoring` | `kube-prometheus-stack-prometheus:9090` |
| Grafana | `monitoring` | `kube-prometheus-stack-grafana:80` |
| Loki | `monitoring` | `loki.monitoring:3100` |
| Tempo | `monitoring` | `tempo.monitoring:3100` |
| AlertManager | `monitoring` | `kube-prometheus-stack-alertmanager:9093` |

```bash
# Grafana (default datasources: Prometheus, Loki, Tempo)
kubectl -n monitoring port-forward svc/kube-prometheus-stack-grafana 3000:80

# Check the stack is healthy
kubectl -n monitoring get pods
```

Retention: Loki and Tempo 7 days, Prometheus per its PVC. Microservice Helm charts
define ServiceMonitors that Prometheus Operator discovers automatically.

## ACA path

ACA streams container logs to the Log Analytics workspace `sb-law-dev-weu`. There
is no in-cluster Prometheus/Grafana — query logs and metrics with KQL.

```bash
# Tail recent logs for one app
az monitor log-analytics query \
  --workspace "$(az monitor log-analytics workspace show -g sb-rg-dev-weu -n sb-law-dev-weu --query customerId -o tsv)" \
  --analytics-query 'ContainerAppConsoleLogs_CL | where ContainerAppName_s == "sb-ca-devicemanager-dev" | order by TimeGenerated desc | take 100'
```

Per-app revision status and replica health are visible in the Container Apps blade
or via `az containerapp revision list`. Set an ingestion cap on the workspace to
keep cost near the free tier.

## Alerts

Alert rules live with the AKS observability stack (`kube-prometheus-stack`
AlertManager). The ACA path has no built-in alerting; wire Azure Monitor alerts on
the Log Analytics workspace if needed.
