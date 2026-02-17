# Web UI Helm Chart

Deploys the SignalBeam Web UI — a React SPA served via nginx with API proxy to the gateway.

## Quick Start

```bash
# Build the Docker image
cd web && docker build -t ghcr.io/signalbeam-io/web:latest .

# Deploy
helm install web deploy/charts/web -n signalbeam --create-namespace
```

## Environment Values

| File | Replicas | HPA | Ingress |
|------|----------|-----|---------|
| `values.yaml` | 2 | 2-8 | off |
| `values-dev.yaml` | 1 | off | off |
| `values-staging.yaml` | 2 | 2-4 | on |
| `values-prod.yaml` | 2 | 2-8 | on + TLS |

## How It Works

1. **Build stage** — `npm run build` produces static assets in `/dist`
2. **Runtime** — nginx serves the SPA with:
   - SPA fallback (`try_files $uri /index.html`)
   - Gzip compression for JS/CSS/JSON
   - CDN-friendly caching (`Cache-Control: public, immutable` for hashed assets, no-cache for `index.html`)
   - Security headers (X-Frame-Options, X-Content-Type-Options, X-XSS-Protection)
   - `/api/` proxy to the API Gateway (configurable via `API_URL` env var)

## Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| `apiUrl` | `http://api-gateway.signalbeam` | Backend API Gateway URL for nginx proxy |
| `service.port` | `80` | Service port |
| `ingress.enabled` | `false` | Enable Ingress |

The `API_URL` environment variable is injected at container startup via nginx's `envsubst` template mechanism — no rebuild needed to change the backend URL.

## Upgrade / Uninstall

```bash
helm upgrade web deploy/charts/web -n signalbeam \
  -f deploy/charts/web/values-prod.yaml

helm uninstall web -n signalbeam
```
