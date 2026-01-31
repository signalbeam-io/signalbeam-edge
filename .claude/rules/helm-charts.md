---
paths:
  - "**/charts/**/*.yaml"
  - "**/charts/**/*.yml"
  - "**/charts/**/*.tpl"
---

# Helm Chart Rules

## Namespace
- All resources deploy to namespace `signalbeam`
- Use `{{ .Release.Namespace }}` in templates

## Standard Labels
- `app.kubernetes.io/name`, `app.kubernetes.io/instance`, `app.kubernetes.io/version`
- `app.kubernetes.io/managed-by: Helm`

## Chart Structure
```
charts/
├── Chart.yaml          # name, version, appVersion
├── values.yaml         # defaults (image, replicas, resources, env)
└── templates/
    ├── deployment.yaml
    ├── service.yaml
    ├── ingress.yaml
    ├── configmap.yaml
    ├── hpa.yaml
    └── _helpers.tpl
```

## Conventions
- Service names match microservice names: `device-manager`, `bundle-orchestrator`, `telemetry-processor`
- Health check paths: `/health/live` (liveness), `/health/ready` (readiness)
- Resource limits always defined in values.yaml
- Environment variables from ConfigMaps and Secrets, not hardcoded
- Use `{{ include "chart.fullname" . }}` from _helpers.tpl for resource naming
