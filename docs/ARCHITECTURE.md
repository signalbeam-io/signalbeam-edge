# SignalBeam Architecture

## Overview

SignalBeam is a cloud-native, multi-tenant edge device management platform built with microservices architecture and modern authentication flows.

## System Architecture

```
┌─────────────────┐
│  Web Frontend   │
│  (React/Vite)   │
│  Port: 5173     │
└────────┬────────┘
         │
         │ All HTTP/OIDC requests
         ▼
┌─────────────────────────────────┐
│      API Gateway (YARP)         │
│      Port: 8080                 │
│  ┌───────────────────────────┐  │
│  │  Route Mapping:           │  │
│  │  /api/devices/*           │──┼──▶ DeviceManager (5296)
│  │  /api/bundles/*           │──┼──▶ BundleOrchestrator (5002)
│  │  /api/heartbeat/*         │──┼──▶ TelemetryProcessor (5003)
│  │  /api/auth/*              │──┼──▶ IdentityManager (5004)
│  │  /api/subscriptions/*     │──┼──▶ IdentityManager (5004)
│  │  /.well-known/*           │──┼──▶ Zitadel (9080)
│  │  /oauth/*                 │──┼──▶ Zitadel (9080)
│  │  /authorize               │──┼──▶ Zitadel (9080)
│  └───────────────────────────┘  │
└─────────────────────────────────┘
         │
         ├──────────────────────────┐
         │                          │
         ▼                          ▼
┌──────────────────┐      ┌──────────────────┐
│  Zitadel OIDC    │      │  Microservices   │
│  Port: 9080      │      │                  │
│                  │      │  - DeviceManager │
│  Endpoints:      │      │  - BundleOrch.   │
│  /.well-known/   │      │  - Telemetry     │
│  /oauth/v2/*     │      │  - Identity      │
│  /authorize      │      │                  │
└──────────────────┘      └──────────────────┘
         │
         ▼
┌──────────────────┐
│  PostgreSQL      │
│  (TimescaleDB)   │
│  Port: 5432      │
└──────────────────┘
```

## Port Allocation

| Service | Port | Purpose |
|---------|------|---------|
| Web Frontend | 5173 | Vite dev server |
| **API Gateway** | **8080** | Single entry point for all services |
| Zitadel | 9080 | OIDC provider (proxied via gateway) |
| DeviceManager | 5296 | Device CRUD and registration |
| BundleOrchestrator | 5002 | Bundle management and rollouts |
| TelemetryProcessor | 5003 | Metrics and heartbeat processing |
| IdentityManager | 5004 | User auth, tenant, subscription mgmt |
| PostgreSQL | 5432 | Database |
| NATS | 4222 | Message broker |
| NATS Monitoring | 8222 | NATS web UI |
| Valkey | 6379 | Distributed cache |

## OIDC Authentication Flow

### Architecture Decision: Proxy OIDC through API Gateway

**Why?**
1. ✅ **Single Entry Point**: All frontend traffic goes through one URL
2. ✅ **Simplified CORS**: Only API Gateway needs CORS configuration
3. ✅ **Consistent URLs**: Same base URL for API and auth
4. ✅ **Production Ready**: Easy to change Zitadel backend without frontend changes
5. ✅ **Security**: Zitadel can be on internal network, not exposed publicly

**How it works:**

```
Frontend (http://localhost:5173)
    │
    │ 1. User clicks "Sign in with Zitadel"
    │    Authority: http://localhost:8080 (API Gateway)
    │
    ▼
API Gateway (http://localhost:8080)
    │
    │ 2. Frontend requests /.well-known/openid-configuration
    │    Gateway proxies to Zitadel
    │
    ▼
Zitadel (http://localhost:9080)
    │
    │ 3. Returns OIDC configuration (with URLs pointing to gateway)
    │
    ▼
Frontend
    │
    │ 4. Frontend redirects to /authorize (through gateway)
    │
    ▼
API Gateway → Zitadel
    │
    │ 5. User authenticates in Zitadel UI
    │
    ▼
Zitadel redirects back
    │
    │ 6. Callback: http://localhost:5173/callback?code=...
    │
    ▼
Frontend
    │
    │ 7. Exchange code for token: POST /oauth/v2/token (via gateway)
    │
    ▼
API Gateway → Zitadel → Issues JWT
    │
    │ 8. Frontend receives access_token
    │
    ▼
Frontend
    │
    │ 9. Call GET /api/auth/me with Bearer token
    │
    ▼
API Gateway → IdentityManager
    │
    │ 10. Returns user + tenant context or 404 (new user)
    │
    ▼
Frontend
    │
    │ 11a. If existing user: Navigate to dashboard
    │ 11b. If new user: Navigate to /register
    │
    ▼
Registration (if new user)
    │
    │ 12. POST /api/auth/register with tenant details
    │
    ▼
API Gateway → IdentityManager
    │
    │ 13. Create tenant + user + subscription
    │ 14. Return full user context
    │
    ▼
Frontend → Dashboard with tenant context
```

## Configuration

### Frontend (.env.development)

```env
# API Gateway is the single entry point
VITE_API_URL=http://localhost:8080

# Zitadel authority points to API Gateway (not direct Zitadel!)
VITE_ZITADEL_AUTHORITY=http://localhost:8080
VITE_ZITADEL_CLIENT_ID=<from-zitadel-console>
VITE_ZITADEL_REDIRECT_URI=http://localhost:5173/callback
VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI=http://localhost:5173
```

### API Gateway (appsettings.json)

```json
{
  "ReverseProxy": {
    "Routes": {
      "oidc-wellknown-route": {
        "ClusterId": "zitadel",
        "Match": { "Path": "/.well-known/{**catch-all}" }
      },
      "oidc-oauth-route": {
        "ClusterId": "zitadel",
        "Match": { "Path": "/oauth/{**catch-all}" }
      }
    },
    "Clusters": {
      "zitadel": {
        "Destinations": {
          "destination1": { "Address": "http://localhost:9080" }
        }
      }
    }
  }
}
```

## Microservices Architecture

### 1. DeviceManager

**Responsibilities:**
- Device CRUD operations
- Device registration with quota validation
- Device grouping and tagging
- Registration token management
- Device API key management

**Database Schema:** `device_manager`

**Key Dependencies:**
- IdentityManager (for quota checks)
- NATS (publish device events)

### 2. BundleOrchestrator

**Responsibilities:**
- App Bundle CRUD and versioning
- Bundle assignment to devices/groups
- Rollout orchestration and tracking
- Phased rollout management

**Database Schema:** `bundle_orchestrator`

**Key Dependencies:**
- Azure Blob Storage (bundle artifacts)
- NATS (publish rollout events)

### 3. TelemetryProcessor

**Responsibilities:**
- Process device heartbeats
- Store metrics in TimescaleDB
- Track device online/offline status
- Aggregate metrics for dashboards

**Database Schema:** `telemetry_processor`

**Key Dependencies:**
- TimescaleDB (time-series data)
- NATS (consume heartbeat messages)

### 4. IdentityManager

**Responsibilities:**
- User authentication and registration
- Tenant (workspace) management
- Subscription tier management
- Quota enforcement (device limits, data retention)
- JWT token validation and enrichment

**Database Schema:** `identity`

**Key Dependencies:**
- Zitadel (OIDC provider)
- PostgreSQL (user/tenant/subscription data)

### 5. API Gateway (YARP)

**Responsibilities:**
- Single entry point for all HTTP traffic
- Route requests to appropriate microservices
- Proxy OIDC flows to Zitadel
- CORS handling
- (Future: Rate limiting, API versioning)

**No Database**

## Data Flow Examples

### Device Registration with Quota Check

```
Edge Agent
    │
    │ POST /api/devices (tenantId, deviceName, registrationToken)
    ▼
API Gateway → DeviceManager
    │
    │ 1. Validate registration token
    │ 2. Check device quota
    │
    ▼
DeviceManager → IdentityManager
    │
    │ POST /api/subscriptions/check-device-quota
    │ (tenantId)
    │
    ▼
IdentityManager
    │
    │ 1. Get tenant
    │ 2. Get active subscription
    │ 3. Check: currentDeviceCount < maxDevices?
    │
    ▼
Return: Success or QuotaExceeded
    │
    ▼
DeviceManager
    │
    │ If quota OK: Create device
    │ If quota exceeded: Return 400 BadRequest
    │
    ▼
Response to Edge Agent
```

### User Registration and Workspace Creation

```
Frontend
    │
    │ 1. User logs in via Zitadel (through gateway)
    │ 2. GET /api/auth/me → 404 (new user)
    │ 3. Navigate to /register
    │
    ▼
Registration Page
    │
    │ POST /api/auth/register
    │ {
    │   email, name, zitadelUserId,
    │   tenantName, tenantSlug
    │ }
    │
    ▼
API Gateway → IdentityManager
    │
    │ RegisterUserHandler:
    │
    │ 1. Validate slug is unique
    │ 2. Create Tenant (Free tier, 5 devices, 7 days retention)
    │ 3. Create User (Admin role)
    │ 4. Create Subscription (Active, 0 devices)
    │ 5. Atomic transaction
    │
    ▼
Return: User + Tenant + Subscription details
    │
    ▼
Frontend
    │
    │ Store in auth state
    │ Navigate to dashboard
```

## Security Considerations

### Multi-Tenancy

- ✅ Every request includes tenant context (from JWT claims)
- ✅ All database queries filter by tenant_id
- ✅ API endpoints validate tenant ownership
- ✅ Row-Level Security (future enhancement)

### Authentication

- ✅ JWT Bearer tokens from Zitadel
- ✅ Tokens validated on every request
- ✅ Claims enriched with tenant_id and role
- ✅ API Key auth for edge devices (legacy, device-specific)

### Authorization

- ✅ Role-based access control (Admin, DeviceOwner)
- ✅ Tenant isolation (users can only access their tenant's data)
- ✅ Quota enforcement (device limits, data retention)

## Scalability & Performance

### Horizontal Scaling

All microservices are stateless and can scale horizontally:

```bash
# Scale DeviceManager to 3 replicas
kubectl scale deployment device-manager --replicas=3
```

### Database Optimization

- ✅ TimescaleDB for time-series data (10-100x faster queries)
- ✅ Indexes on tenant_id for all multi-tenant queries
- ✅ Connection pooling (Npgsql)
- ✅ Read replicas (future)

### Caching Strategy

- ✅ Valkey (Redis) for:
  - Session data
  - User/tenant metadata (short TTL)
  - Frequently accessed device lists
  - Subscription quota cache

### Message Queue

- ✅ NATS with JetStream for:
  - Event-driven architecture
  - Async processing (heartbeats, rollouts)
  - Decoupling services
  - Guaranteed delivery (JetStream persistence)

## Deployment

### Local Development

```bash
# 1. Start infrastructure
docker-compose -f docker-compose.dev.yml up -d

# 2. Start backend (Aspire)
cd src/SignalBeam.AppHost
dotnet run

# 3. Start frontend
cd web
npm run dev
```

### Production (Kubernetes)

```bash
# 1. Deploy infrastructure chart
helm install signalbeam-infra deploy/charts/signalbeam-infrastructure

# 2. Deploy platform chart
helm install signalbeam deploy/charts/signalbeam-platform

# 3. Verify deployment
kubectl get pods -n signalbeam
```

## Monitoring & Observability

### Metrics (Prometheus)

- Service health (uptime, error rates)
- Request latency (p50, p95, p99)
- Database query performance
- Device registration rate
- Quota usage per tenant

### Logs (Loki / OpenObserve)

- Structured logging with Serilog
- Correlation IDs for request tracing
- Log aggregation from all services

### Traces (Tempo / Jaeger)

- OpenTelemetry instrumentation
- Distributed tracing across microservices
- Database query tracing

### Dashboards (Grafana)

- System health overview
- Tenant usage metrics
- Device fleet status
- Rollout progress

## Detailed Documentation

For in-depth information about specific aspects of the architecture, see:

### Authentication & Multi-Tenancy
- **[Authentication Architecture](./architecture/AUTHENTICATION_ARCHITECTURE.md)** - Deep dive into authentication flows, JWT validation, multi-tenancy, and security architecture
- **[Authentication Feature Guide](./features/AUTHENTICATION.md)** - Complete feature documentation including user flows, API reference, configuration, and troubleshooting
- **[Zitadel Aspire Setup](./ZITADEL_ASPIRE_SETUP.md)** - Step-by-step guide for running Zitadel with .NET Aspire for local development
- **[Frontend Authentication Guide](../web/AUTHENTICATION.md)** - Frontend-specific authentication configuration and flows

### Getting Started
- **[Zitadel Setup Guide](./ZITADEL_SETUP.md)** - Production Zitadel deployment and configuration
- **[CLAUDE.md](../CLAUDE.md)** - Project overview, technical stack, and development guidelines

## Future Enhancements

- [ ] gRPC inter-service communication
- [ ] Event Sourcing for audit trail
- [ ] CQRS with read models
- [ ] GraphQL API gateway
- [ ] WebSocket support for real-time updates
- [ ] Multi-region deployment
- [ ] Database sharding by tenant
- [ ] CDN for bundle artifacts
- [ ] Advanced analytics with ClickHouse
- [ ] Team management and user invitations
- [ ] Custom roles and permissions
- [ ] SSO integration (SAML, Google Workspace)
- [ ] API keys for service accounts
- [ ] Comprehensive audit logs
- [ ] Billing integration (Stripe)
