---
name: add-helm-chart
description: Scaffold a new Helm chart with standard templates and values
allowed-tools: Bash, Write, Read, Glob
user-invocable: true
---

# Add Helm Chart

Scaffold a new Helm chart following project conventions.

## Arguments

- `{chart-name}` — Name of the chart (required, e.g., `signalbeam-platform`, `device-manager`)

## Process

1. Create the chart directory structure under `deploy/charts/{chart-name}/`:

```
deploy/charts/{chart-name}/
├── Chart.yaml
├── values.yaml
├── templates/
│   ├── _helpers.tpl
│   ├── deployment.yaml
│   ├── service.yaml
│   ├── configmap.yaml
│   ├── ingress.yaml
│   └── hpa.yaml
```

2. Read `.claude/rules/helm-charts.md` for project conventions.

3. Generate files following these conventions:
   - Namespace: `signalbeam`
   - Standard Kubernetes labels: `app.kubernetes.io/name`, `app.kubernetes.io/instance`, `app.kubernetes.io/version`
   - Health check paths: `/health/live` (liveness), `/health/ready` (readiness)
   - Resource limits and requests with sensible defaults
   - HPA with min 1, max 3 replicas

4. **Chart.yaml** template:
```yaml
apiVersion: v2
name: {chart-name}
description: {description}
type: application
version: 0.1.0
appVersion: "0.1.0"
```

5. **values.yaml** — Include configurable values for:
   - `image.repository`, `image.tag`, `image.pullPolicy`
   - `replicaCount`
   - `resources.requests` and `resources.limits`
   - `service.type`, `service.port`
   - `ingress.enabled`, `ingress.hosts`
   - `env` (environment variables as key-value map)

6. Validate the chart:
```bash
helm lint deploy/charts/{chart-name}
helm template test deploy/charts/{chart-name} > /dev/null
```

## After Scaffolding

Report what was created and remind to:
- Customize `values.yaml` for the specific service
- Add environment-specific value overrides if needed
- Update any umbrella chart dependencies if applicable
