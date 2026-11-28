<!-- Generated from code by /docs on 2026-11-28. Do not edit generated sections. -->
# BundleOrchestrator API Reference

BundleOrchestrator manages app bundles and their versions, assigns bundles to devices and groups,
runs rollouts, and serves the two endpoints edge agents call to reconcile. Through the gateway,
these route prefixes reach it: `/api/bundles/*`, `/api/rollouts/*`, `/api/phased-rollouts/*`,
`/api/device-groups/*`, and three device sub-resources that take precedence over DeviceManager's
`/api/devices/*` catch-all: `/api/devices/{deviceId}/desired-state`, `/reconciliation-status` and `/bundle`.

See: `src/SignalBeam.ApiGateway/appsettings.json`, `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/Endpoints/`

## Authentication and authorization

<!-- BEGIN GENERATED -->
`UseApiKeyAuthentication()` authenticates every request except `/health`, `/metrics`, `/scalar` and `/openapi`:

| Credential | How it is checked | `auth_method` |
|------------|-------------------|---------------|
| `Authorization: Bearer <jwt>` | Zitadel JWT (issuer, audience, lifetime, signature). An invalid token is `401` | `Jwt` |
| `X-Api-Key: sb_device_…` | Must be exactly `sb_device_{8 hex}_{40 [a-z0-9]}`, then verified by DeviceManager's internal endpoint (cached up to 2 min). Malformed or rejected is `401`; DeviceManager unreachable is `503` with `Retry-After: 5` | `DeviceApiKey` |
| `X-Api-Key: <tenant key>` | Config-backed tenant key | `TenantApiKey` |

Each endpoint then requires one of these policies:

| Policy | JWT | Device key | Tenant key |
|--------|-----|------------|------------|
| `OperatorAccess` | Yes | Never | Outside Production only |
| `DeviceOrOperatorAccess` | Yes | Only for the route's `{deviceId}` | Outside Production only |
| `DeviceAccess` | No | Only for the route's `{deviceId}` | Outside Production only |

A missing credential is `401` (`missing_api_key`); a credential the policy refuses is `403`.

Rate limiting: 100 requests per minute, with up to 10 queued, per device (device keys) or per tenant
(everyone else). Over the limit is `429` with `{ "error": "RATE_LIMIT_EXCEEDED", "message", "retryAfter" }`.

See: `src/Shared/SignalBeam.Shared.Infrastructure/Authentication/ApiKeyAuthenticationMiddleware.cs`,
`src/Shared/SignalBeam.Shared.Infrastructure/Authentication/Authorization/`, `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/Program.cs`
<!-- END GENERATED -->

## Endpoint summary

<!-- BEGIN GENERATED -->
| Method | Route | Description | Policy |
|--------|-------|-------------|--------|
| GET | `/api/devices/{deviceId}/desired-state` | Bundle and containers the device should run | `DeviceOrOperatorAccess` |
| POST | `/api/devices/{deviceId}/reconciliation-status` | Device reports the outcome of reconciling | `DeviceAccess` |
| POST | `/api/devices/{deviceId}/bundle` | Assign a bundle version to a device | `OperatorAccess` |
| POST | `/api/device-groups/{deviceGroupId}/bundle` | Assign a bundle version to every device in a group | `OperatorAccess` |
| POST | `/api/bundles` | Create a bundle, optionally with a first version | `OperatorAccess` |
| POST | `/api/bundles/upload` | Create a bundle from a full bundle definition | `OperatorAccess` |
| GET | `/api/bundles` | List a tenant's bundles | `OperatorAccess` |
| GET | `/api/bundles/{bundleId}` | Bundle with all its versions | `OperatorAccess` |
| GET | `/api/bundles/{bundleId}/assigned-devices` | Devices whose desired state is this bundle | `OperatorAccess` |
| POST | `/api/bundles/{bundleId}/versions` | Add a version from a bundle definition | `OperatorAccess` |
| GET | `/api/bundles/{bundleId}/versions/latest` | Latest bundle definition | `OperatorAccess` |
| GET | `/api/bundles/{bundleId}/versions/{version}` | Bundle definition for a version | `OperatorAccess` |
| GET | `/api/bundles/{bundleId}/versions/{version}/download` | Redirect to a time-limited download URL | `OperatorAccess` |
| GET | `/api/rollouts` | Paginated list of rollouts | `OperatorAccess` |
| POST | `/api/rollouts` | Assign a bundle to many devices or groups at once | `OperatorAccess` |
| GET | `/api/rollouts/{rolloutId}` | Rollout with progress counts | `OperatorAccess` |
| GET | `/api/rollouts/{rolloutId}/devices` | Per-device status in a rollout | `OperatorAccess` |
| POST | `/api/rollouts/{rolloutId}/cancel` | Cancel pending and in-progress devices | `OperatorAccess` |
| GET | `/api/rollouts/bundles/{bundleId}` | Rollout status of every device for a bundle | `OperatorAccess` |
| PUT | `/api/rollouts/{rolloutId}/status` | Set one device rollout status record | `OperatorAccess` |
| GET | `/api/phased-rollouts` | Paginated list of phased rollouts | `OperatorAccess` |
| POST | `/api/phased-rollouts` | Create a phased rollout | `OperatorAccess` |
| GET | `/api/phased-rollouts/active` | In-progress and paused rollouts | `OperatorAccess` |
| GET | `/api/phased-rollouts/{rolloutId}` | Rollout with phases and device assignments | `OperatorAccess` |
| GET | `/api/phased-rollouts/bundles/{bundleId}/history` | Rollout history of a bundle | `OperatorAccess` |
| POST | `/api/phased-rollouts/{rolloutId}/start` | Start a pending rollout | `OperatorAccess` |
| POST | `/api/phased-rollouts/{rolloutId}/pause` | Pause an in-progress rollout | `OperatorAccess` |
| POST | `/api/phased-rollouts/{rolloutId}/resume` | Resume a paused rollout | `OperatorAccess` |
| POST | `/api/phased-rollouts/{rolloutId}/advance` | Complete the current phase and start the next | `OperatorAccess` |
| POST | `/api/phased-rollouts/{rolloutId}/rollback` | Roll devices back to the previous version | `OperatorAccess` |
<!-- END GENERATED -->

All error bodies have the shape `{ "error": "CODE", "message": "...", "type": "ErrorType" }` unless
noted. Status codes are set per endpoint group, not from the error type everywhere, so each section
lists them.

## Tenant scoping

Most operator endpoints don't check the caller's tenant. `GET /api/bundles`, `GET /api/phased-rollouts`
and `GET /api/phased-rollouts/active` filter by a `tenantId` **query parameter** the caller supplies;
endpoints addressed by a bundle, rollout or group ID return it whatever its tenant; and
`GET /api/rollouts` returns rollouts of every tenant with `tenantId` set to an empty GUID. Only the two
device endpoints check the caller against the device's tenant. `POST /api/bundles` and
`POST /api/bundles/upload` take the tenant from the `tenantId` query parameter, falling back to the
caller's `tenant_id` claim.

## Device endpoints

<!-- BEGIN GENERATED -->
### GET /api/devices/{deviceId}/desired-state

Returns the bundle version assigned to the device, with its containers in the shape the edge agent
runs. `{deviceId}` must be a GUID.

A device key may only read its own device. JWT and tenant-key callers must belong to the device's
tenant, checked through DeviceManager's internal tenant lookup; another tenant's device reads as `404`.

**Response (200):**
```json
{
  "deviceId": "guid",
  "bundleId": "guid",
  "bundleVersion": "1.4.0",
  "assignedAt": "datetime",
  "assignedBy": "string | null",
  "containers": [
    {
      "name": "web",
      "image": "nginx:1.27",
      "environment": { "MODE": "edge" },
      "ports": { "80/tcp": "8080" },
      "volumes": { "/var/lib/app": "/data", "/etc/app": "/config:ro" }
    }
  ]
}
```

`environment`, `ports` and `volumes` are `null` when the version stores none. `ports` maps
`{containerPort}/{protocol}` to the host port (empty string: Docker picks one); `volumes` maps the host
path to the container path, with `:ro` for read-only mounts.

**Error Codes:**
| Code | Status | Description |
|------|--------|-------------|
| `NO_DESIRED_STATE` | 404 | No bundle is assigned. The agent treats this as "leave the device alone" |
| `DEVICE_NOT_FOUND` | 404 | Device unknown, or in another tenant (non-device callers) |
| `TENANT_REQUIRED` | 403 | Non-device caller has no `tenant_id` claim |
| `DEVICE_LOOKUP_UNAVAILABLE` | 503 | DeviceManager's tenant lookup failed |
| `DESIRED_BUNDLE_VERSION_MISSING` | 500 | The assigned version no longer exists. Deliberately not an empty answer, which would make the agent stop every container |

See: `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Application/Queries/GetDeviceDesiredState.cs`,
`src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Application/Services/DesiredContainerMapper.cs`

### POST /api/devices/{deviceId}/reconciliation-status

The agent reports whether it reconciled its desired bundle. Marks the device's open rollout status
record for that bundle version `Succeeded` or `Failed`. A cancelled rollout stays cancelled.
`{deviceId}` must be a GUID and overrides `deviceId` in the body.

**Request Body** (the agent's `ReconciliationStatus`; extra fields such as `timestamp` and `actions` are ignored):
```json
{
  "deviceId": "guid",
  "status": "succeeded | failed",
  "bundleId": "string | null",
  "bundleVersion": "string | null",
  "errors": ["string"]
}
```

**Response (200):**
```json
{ "deviceId": "guid", "tracked": true, "rolloutStatus": "Succeeded | Failed | Cancelled | null" }
```

`tracked` is `false` when there is no open rollout record for that bundle version, or the bundle
reference is missing or unparseable.

**Error Codes:**
| Code | Status | Description |
|------|--------|-------------|
| `VALIDATION_FAILED` | 400 | A validation rule failed (message lists them) |
| `INVALID_RECONCILIATION_STATUS` | 400 | `status` is not `succeeded` or `failed` |
| `DEVICE_NOT_FOUND` | 404 | Device unknown, or in another tenant (tenant-key callers) |
| `TENANT_REQUIRED` | 403 | Tenant-key caller has no `tenant_id` claim |
| `DEVICE_LOOKUP_UNAVAILABLE` | 503 | DeviceManager's tenant lookup failed |

**Validation Rules:**
- `status`: `succeeded` or `failed`, case-insensitive
- `bundleId`, `bundleVersion`: at most 100 characters
- `errors`: at most 50 entries of at most 1000 characters; joined and truncated to 2000 characters when stored

See: `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Application/Commands/ReportReconciliationStatus.cs`,
`src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Application/Validators/ReportReconciliationStatusValidator.cs`
<!-- END GENERATED -->

## Bundle assignment

<!-- BEGIN GENERATED -->
### POST /api/devices/{deviceId}/bundle

Sets the device's desired state to a bundle version. `{deviceId}` overrides the body.

**Request Body:**
```json
{ "bundleId": "guid", "version": "1.4.0", "assignedBy": "string | null" }
```

**Response (200):**
```json
{ "deviceId": "guid", "bundleId": "guid", "version": "1.4.0", "assignedAt": "datetime" }
```

**Error Codes** (all `400`): `INVALID_DEVICE_ID`, `INVALID_BUNDLE_ID`, `INVALID_VERSION`, `BUNDLE_NOT_FOUND`, `VERSION_NOT_FOUND`.

### POST /api/device-groups/{deviceGroupId}/bundle

Sets the desired state of every device in the group. `{deviceGroupId}` overrides the body.

**Request Body:**
```json
{ "bundleId": "guid", "version": "1.4.0", "assignedBy": "string | null" }
```

**Response (200):**
```json
{ "deviceGroupId": "guid", "bundleId": "guid", "version": "1.4.0", "deviceCount": 12, "assignedAt": "datetime" }
```

**Error Codes** (all `400`): `INVALID_GROUP_ID`, `INVALID_BUNDLE_ID`, `INVALID_VERSION`, `BUNDLE_NOT_FOUND`, `VERSION_NOT_FOUND`, `GROUP_NOT_FOUND`, `EMPTY_GROUP`.

See: `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/Endpoints/BundleAssignmentEndpoints.cs`
<!-- END GENERATED -->

## Bundles

<!-- BEGIN GENERATED -->
Failures are `400` on the write endpoints and `404` on the reads below, whatever the error type.

### POST /api/bundles

Query: `tenantId` (optional GUID; defaults to the caller's `tenant_id` claim).

**Request Body:**
```json
{
  "name": "string",
  "description": "string | null",
  "version": "1.0.0 | null",
  "containers": [
    {
      "name": "web",
      "image": "nginx",
      "tag": "1.27",
      "environment": { "KEY": "value" },
      "ports": [{ "host": 8080, "container": 80, "protocol": "tcp" }],
      "volumes": [{ "hostPath": "/data", "containerPath": "/data", "readOnly": false }]
    }
  ]
}
```

`image` and `tag` are joined as `{image}:{tag}` (`latest` when `tag` is omitted). Ports are stored as
`"{host}:{container}/{protocol}"`. `volumes` is accepted but not stored by this endpoint.

**Response (201):** `{ "bundleId", "name", "description", "createdAt", "version", "versionId" }` (`version`/`versionId` null without containers).

**Error Codes:** `INVALID_TENANT_ID`, `VALIDATION_FAILED`, `INVALID_VERSION`, `INVALID_CONTAINER_SPEC`.

**Validation Rules:**
- `name`: required, at most 200 characters; `description`: at most 1000
- `version` and `containers` both present or both omitted; `version` a semantic version
- each container: `name` required (at most 100), `image` required (at most 500)

### POST /api/bundles/upload

Creates a bundle and its first version from a full bundle definition, stored in blob storage.
Query: `tenantId` (optional GUID; defaults to the caller's `tenant_id` claim).

**Request Body** (`BundleDefinition`):
```json
{
  "bundleId": "string",
  "version": "1.2.0",
  "description": "string | null",
  "checksum": "sha256:<64 hex> (optional)",
  "containers": [
    {
      "name": "sensor",
      "image": "ghcr.io/signalbeam/temp-sensor:1.2.0",
      "imagePullPolicy": "IfNotPresent",
      "env": { "KEY": "value" },
      "ports": [{ "containerPort": 8080, "hostPort": 18080, "protocol": "tcp" }],
      "resources": { "limits": { "memory": "128Mi", "cpu": "0.5" }, "requests": null },
      "volumes": [{ "name": "data", "hostPath": "/var/lib/sensor", "containerPath": "/data", "readOnly": false }],
      "restartPolicy": "always"
    }
  ]
}
```

**Response (201):** `{ "bundleId", "version", "blobStorageUri", "checksum", "sizeBytes", "createdAt" }`.

**Error Codes:** `INVALID_TENANT_ID`, `INVALID_BUNDLE_DEFINITION`, `INVALID_BUNDLE_ID`, `INVALID_VERSION`, `INVALID_CONTAINER_SPEC`, `BUNDLE_ALREADY_EXISTS`.

**Validation Rules** (`BundleDefinitionValidator`, run by the handler):
- `bundleId`: a GUID or `[a-z0-9-]+`; `version`: `^\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$`
- `checksum`, when set: `sha256:` followed by 64 lowercase hex characters
- at least one container; container `name`: `[a-z0-9-]+`
- container `image`: from `ghcr.io`, `docker.io`, `mcr.microsoft.com`, `quay.io` or `localhost`

### GET /api/bundles

Query: `tenantId` (required GUID).

**Response (200):**
```json
{
  "bundles": [
    {
      "bundleId": "guid", "name": "string", "description": "string | null",
      "latestVersion": "string | null", "createdAt": "datetime",
      "versions": [ { "versionId": "guid", "version": "1.0.0", "containers": [ContainerSpecDetail], "containerCount": 1, "releaseNotes": "string | null", "createdAt": "datetime" } ]
    }
  ]
}
```

`ContainerSpecDetail` is the stored form: `{ "name", "image", "environmentVariables", "portMappings", "volumeMounts", "additionalParameters" }`, the last four as JSON strings.

### GET /api/bundles/{bundleId}

**Response (200):** `{ "bundle": { "bundleId", "name", "description", "latestVersion", "createdAt", "versions": [...] } }`, with versions as in `GET /api/bundles`.

**Error Codes** (all `404`): `INVALID_BUNDLE_ID`, `BUNDLE_NOT_FOUND`.

### GET /api/bundles/{bundleId}/assigned-devices

**Response (200):**
```json
{ "bundleId": "guid", "assignedDevices": [ { "deviceId": "guid", "bundleVersion": "1.0.0", "assignedAt": "datetime", "assignedBy": "string | null" } ] }
```

**Error Codes** (all `404`): `INVALID_BUNDLE_ID`, `BUNDLE_NOT_FOUND`.

See: `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/Endpoints/BundleEndpoints.cs`,
`src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Application/Models/BundleDefinition.cs`,
`src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Application/Validators/`
<!-- END GENERATED -->

## Bundle versions

<!-- BEGIN GENERATED -->
### POST /api/bundles/{bundleId}/versions

Adds a version from a `BundleDefinition` (same body and validation as `POST /api/bundles/upload`).

**Response (201):** `{ "versionId", "bundleId", "version", "blobStorageUri", "checksum", "sizeBytes", "createdAt" }`.

**Error Codes** (all `400`): `INVALID_BUNDLE_ID`, `INVALID_BUNDLE_DEFINITION`, `INVALID_VERSION`, `INVALID_CONTAINER_SPEC`, `BUNDLE_NOT_FOUND`.

### GET /api/bundles/{bundleId}/versions/{version} and /latest

Returns the stored `BundleDefinition` (read from blob storage), with `checksum` set from the version record.

Caching: the response carries `ETag: "{checksum}"`, `Cache-Control: public, max-age=300` and
`Last-Modified`. A matching `If-None-Match` returns `304`.

**Error Codes** (all `404`): `INVALID_BUNDLE_ID`, `INVALID_VERSION`, `BUNDLE_NOT_FOUND`, `VERSION_NOT_FOUND`, `LATEST_VERSION_NOT_FOUND` (latest only), `BUNDLE_DEFINITION_MISSING`, `BUNDLE_DEFINITION_ERROR`.

### GET /api/bundles/{bundleId}/versions/{version}/download

Redirects (`302`) to a blob download URL valid for one hour.

**Error Codes:** `INVALID_BUNDLE_ID` (400), `INVALID_VERSION` (400), `BUNDLE_NOT_FOUND` (404), `VERSION_NOT_FOUND` (404). These bodies have no `type`.

See: `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/Endpoints/BundleVersionEndpoints.cs`
<!-- END GENERATED -->

## Rollouts

<!-- BEGIN GENERATED -->
### GET /api/rollouts

Query: `bundleId` (optional), `page` (default 1), `pageSize` (default 10).

**Response (200):**
```json
{
  "data": [
    {
      "id": "guid", "tenantId": "00000000-0000-0000-0000-000000000000", "bundleId": "guid",
      "version": "1.0.0", "targetType": "device", "targetIds": ["string"], "status": "string",
      "progress": { "total": 0, "pending": 0, "inProgress": 0, "succeeded": 0, "failed": 0, "cancelled": 0 },
      "createdAt": "datetime", "updatedAt": "datetime", "completedAt": "datetime | null"
    }
  ],
  "page": 1, "pageSize": 10, "totalCount": 0, "totalPages": 0
}
```

`tenantId` is always the empty GUID and `targetType` always `device` (both TODO in `GetRollouts.cs`).

**Error Codes:** `INVALID_BUNDLE_ID` (400).

### POST /api/rollouts

**Request Body:**
```json
{ "bundleId": "guid", "version": "1.0.0", "targetType": "device | group", "targetIds": ["guid"], "assignedBy": "string | null" }
```

**Response (201):** `{ "rolloutId", "bundleId", "version", "targetType", "totalDevices", "createdAt" }`.

**Error Codes** (all `400`): `INVALID_BUNDLE_ID`, `INVALID_VERSION`, `INVALID_TARGET_TYPE`, `INVALID_DEVICE_ID`, `INVALID_GROUP_ID`, `BUNDLE_NOT_FOUND`, `VERSION_NOT_FOUND`, `GROUP_NOT_FOUND`, `NO_DEVICES`.

### GET /api/rollouts/{rolloutId}

**Response (200):** `{ "rolloutId", "bundleId", "bundleVersion", "status", "progress": { "total", "pending", "inProgress", "succeeded", "failed", "cancelled" }, "createdAt", "completedAt" }`.

### GET /api/rollouts/{rolloutId}/devices

**Response (200):** `[ { "deviceId", "status", "startedAt", "completedAt", "errorMessage", "retryCount" } ]`.

### POST /api/rollouts/{rolloutId}/cancel

**Response:** `204`.

**Error Codes** for these three: `INVALID_ROLLOUT_ID`, `ROLLOUT_NOT_FOUND`; `404` on the two reads and `400` on cancel. These bodies have no `type`.

### GET /api/rollouts/bundles/{bundleId}

**Response (200):**
```json
{
  "rollouts": [ { "rolloutId": "guid", "bundleId": "guid", "bundleVersion": "1.0.0", "deviceId": "guid", "status": "string", "startedAt": "datetime", "completedAt": "datetime | null", "errorMessage": "string | null", "retryCount": 0 } ],
  "totalCount": 0, "pendingCount": 0, "inProgressCount": 0, "succeededCount": 0, "failedCount": 0
}
```

**Error Codes:** `INVALID_BUNDLE_ID` (400).

### PUT /api/rollouts/{rolloutId}/status

Sets the status of one rollout status record. Operator-only; devices report through `reconciliation-status` instead.

**Request Body:**
```json
{ "status": "Pending | InProgress | Succeeded | Failed", "errorMessage": "string | null" }
```

**Response (200):** `{ "rolloutId", "status", "completedAt" }`.

**Error Codes** (all `400`): `UNSUPPORTED_STATUS`, `ERROR_MESSAGE_REQUIRED`, `INVALID_STATUS_TRANSITION`, `ROLLOUT_NOT_FOUND`.

See: `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/Endpoints/RolloutEndpoints.cs`,
`src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/Endpoints/RolloutStatusEndpoints.cs`
<!-- END GENERATED -->

## Phased rollouts

<!-- BEGIN GENERATED -->
Errors map by type: `NotFound` → 404, `Validation` → 400, `Conflict` → 409, `Forbidden` → 403 with no
body, anything else → 500 problem details with `error` and `type` extensions.

### GET /api/phased-rollouts

Query: `tenantId` (required GUID), `page` (required, ≥ 1), `pageSize` (required, 1–100), `status`
(optional: `Pending`, `InProgress`, `Paused`, `Completed`, `Failed`, `RolledBack`, `Cancelled`), `bundleId` (optional).

**Response (200):** `{ "rollouts": [ { "rolloutId", "bundleId", "name", "targetVersion", "status", "currentPhaseNumber", "totalPhases", "totalDevices", "succeededDevices", "failedDevices", "successRate", "createdAt", "startedAt", "completedAt" } ], "page", "pageSize", "totalCount", "totalPages" }`.

**Error Codes:** `INVALID_PAGE`, `INVALID_PAGE_SIZE`, `INVALID_STATUS`, `INVALID_BUNDLE_ID` (400).

### POST /api/phased-rollouts

**Request Body:**
```json
{
  "tenantId": "guid",
  "bundleId": "string",
  "targetVersion": "1.2.0",
  "previousVersion": "1.1.0 | null",
  "name": "string",
  "description": "string | null",
  "targetDeviceGroupId": "guid | null",
  "phases": [ { "name": "canary", "targetPercentage": 10, "minHealthyDurationMinutes": 30 } ],
  "failureThreshold": 0.05,
  "createdBy": "string | null"
}
```

**Response (201):** `{ "rolloutId", "bundleId", "targetVersion", "name", "phaseCount", "createdAt" }`.

**Error Codes:** `INVALID_BUNDLE_ID`, `INVALID_VERSION`, `INVALID_PREVIOUS_VERSION`, `NO_PHASES`, `INVALID_PHASE_PERCENTAGES`, `TARGET_GROUP_REQUIRED`, `NO_DEVICES_IN_GROUP` (400); `BUNDLE_NOT_FOUND`, `VERSION_NOT_FOUND`, `GROUP_NOT_FOUND` (404); `ACTIVE_ROLLOUT_EXISTS` (409); `TENANT_MISMATCH` (403).

### GET /api/phased-rollouts/active

Query: `tenantId` (required GUID).

**Response (200):** `{ "activeRollouts": [ { "rolloutId", "bundleId", "name", "targetVersion", "status", "currentPhaseNumber", "currentPhaseName", "currentPhaseTargetCount", "currentPhaseSuccessCount", "currentPhaseFailureCount", "currentPhaseSuccessRate", "phaseStartedAt", "startedAt", "failureThreshold" } ], "totalCount" }`.

### GET /api/phased-rollouts/{rolloutId}

**Response (200):** the rollout (`rolloutId`, `tenantId`, `bundleId`, `targetVersion`, `previousVersion`,
`name`, `description`, `status`, `targetDeviceGroupId`, `createdBy`, `createdAt`, `startedAt`,
`completedAt`, `failureThreshold`, `currentPhaseNumber`), `progress` (`totalDevicesAcrossPhases`,
`succeededDevices`, `failedDevices`, `overallSuccessRate`, `completedPhases`, `totalPhases`) and
`phases[]`, each with its counts, rates, timing and `deviceAssignments[]` (`deviceId`, `status`,
`assignedAt`, `reconciledAt`, `errorMessage`, `retryCount`).

**Error Codes:** `ROLLOUT_NOT_FOUND` (404).

### GET /api/phased-rollouts/bundles/{bundleId}/history

**Response (200):** `{ "bundleId", "rolloutHistory": [ { "rolloutId", "name", "targetVersion", "previousVersion", "status", "totalPhases", "totalDevices", "succeededDevices", "failedDevices", "successRate", "createdAt", "startedAt", "completedAt", "createdBy" } ], "totalCount" }`.

**Error Codes:** `INVALID_BUNDLE_ID` (400).

### Lifecycle actions

| Route | Response (200) | Error Codes |
|-------|----------------|-------------|
| `POST /{rolloutId}/start` | `{ "rolloutId", "startedAt", "currentPhaseNumber", "currentPhaseName" }` | `ROLLOUT_NOT_FOUND`, `INVALID_OPERATION`, `NO_PHASES`, `NO_DEVICES` |
| `POST /{rolloutId}/pause` | `{ "rolloutId", "currentPhaseNumber", "status" }` | `ROLLOUT_NOT_FOUND`, `INVALID_OPERATION` |
| `POST /{rolloutId}/resume` | `{ "rolloutId", "resumedAt", "currentPhaseNumber", "status" }` | `ROLLOUT_NOT_FOUND`, `INVALID_OPERATION` |
| `POST /{rolloutId}/advance` | `{ "rolloutId", "newPhaseNumber", "newPhaseName", "targetDeviceCount", "advancedAt" }` | `ROLLOUT_NOT_FOUND`, `INVALID_OPERATION`, `NO_NEXT_PHASE`, `NO_DEVICES_AVAILABLE` |
| `POST /{rolloutId}/rollback` | `{ "rolloutId", "previousVersion", "rolledBackAt", "devicesAffected" }` | `ROLLOUT_NOT_FOUND`, `INVALID_OPERATION`, `NO_PREVIOUS_VERSION` |

All routes are under `/api/phased-rollouts`; `{rolloutId}` is a GUID.

See: `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/Endpoints/PhasedRolloutEndpoints.cs`
<!-- END GENERATED -->

## Configuration used by these endpoints

<!-- BEGIN GENERATED -->
| Key | Description | Default | Required |
|-----|-------------|---------|----------|
| `DeviceManager:BaseUrl` | DeviceManager URL for device-key verification and tenant lookups | `http://localhost:5296` | Yes, when deployed |
| `InternalServices:ApiKey` | Shared key for DeviceManager's `/api/internal` endpoints; without it every device request is `503` | — | Yes |
| `Authentication:Jwt:Authority` | Zitadel issuer | — | Yes |
| `Authentication:Jwt:RequireHttpsMetadata` | Require HTTPS for OIDC metadata | `true` | No |
| `ZITADEL_CONFIG_PATH` | File the JWT audience is read from (`backend.audience`), else `Authentication:Jwt:Audience` | — | No |
| `AzureBlobStorage:ServiceUri` / `UseManagedIdentity` / `ConnectionString` / `ContainerName` | Blob storage for bundle definitions (Aspire registers `blobs` instead) | container `bundle-manifests` in code, `signalbeam-bundles` in appsettings | Yes, for bundle uploads and definitions |

See: `src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Infrastructure/DependencyInjection.cs`,
`src/Shared/SignalBeam.Shared.Infrastructure/InternalServices/DeviceCredentialClient.cs`,
`src/BundleOrchestrator/SignalBeam.BundleOrchestrator.Host/appsettings.json`
<!-- END GENERATED -->
