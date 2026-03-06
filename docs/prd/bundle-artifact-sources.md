# PRD: Bundle Artifact Sources

> Generated: 2026-03-06
> Author: Claude Code + marjangjuroski
> Status: Draft

## 1. Executive Summary

SignalBeam Edge currently defines bundles as JSON manifests with container image references (e.g., `ghcr.io/org/app:1.0`), but has no mechanism to source, cache, or deliver the actual container images and binaries to edge devices. This feature adds artifact lifecycle management — pull from private registries, upload via dashboard, cache in Azure Blob Storage, verify with content signatures, and deliver to edge agents through SignalBeam's proxy layer. This is an MVP blocker: without artifact delivery, bundles are metadata without substance.

## 2. Problem Statement

### Current State
- `AppBundleVersion` stores `BlobStorageUri`, `Checksum`, `SizeBytes` but only for JSON manifests
- `ContainerSpec.Image` is a string reference (`ghcr.io/...`) with no credential or source metadata
- EdgeAgent's `ReconciliationService` receives desired state but has no way to pull images from private registries
- No file upload capability exists anywhere in the platform
- Tenant model has no registry configuration

### Business Impact
Without artifact delivery, SignalBeam cannot fulfill its core promise: managing containerized applications on edge devices from a centralized dashboard. Users must manually load images on each device — defeating the purpose of fleet management.

### User Impact
- **Platform admins** cannot deploy software to their fleet without SSH access to each device
- **Edge devices** cannot autonomously pull and run the containers defined in their assigned bundles
- The entire bundle → assignment → rollout pipeline is incomplete

## 3. Goals & Success Metrics

### Goals
- Primary: Enable end-to-end artifact delivery from source (registry/upload) to edge device
- Secondary: Support both container images and raw binaries as artifact types
- Secondary: Provide content trust via artifact signing and verification

### Success Metrics
| Metric | Current | Target | How to Measure |
|--------|---------|--------|----------------|
| Artifacts deliverable to devices | 0 | 100% of assigned bundles | Reconciliation success rate |
| Supported artifact sources | 0 | 2 (registry pull + upload) | Feature completion |
| Artifact integrity verification | None | SHA256 + signature on every delivery | Verification pass rate |
| Time from publish to first device pull | N/A | < 60s for cached artifacts | Telemetry (publish→first pull) |
| Concurrent device pulls supported | 0 | 500 | Load test against Blob SAS endpoints |

### Non-Goals
- Building a full container registry (we proxy/cache, not host)
- Image layer deduplication or delta updates (future optimization)
- Air-gapped deployment without any network (requires USB sideload, out of scope)
- Building images from Dockerfiles (users build externally, we distribute)
- Multi-architecture image resolution (edge devices specify their arch)

## 4. User Stories

### Platform Admin
- As a platform admin, I want to configure my private container registry credentials at the tenant level so that SignalBeam can pull images on behalf of my edge devices
  - Acceptance: Can save ACR/GHCR/DockerHub credentials, credentials are encrypted at rest, used when pulling images
- As a platform admin, I want to upload a binary artifact through the dashboard so that I can distribute non-containerized software to my fleet
  - Acceptance: Can upload files up to 2GB via multipart upload with progress indicator
- As a platform admin, I want to see artifact status (cached, size, checksum) on each bundle version so that I know artifacts are ready for deployment
  - Acceptance: Bundle version detail shows artifact list with status, size, checksum, and cache state

### Bundle Author
- As a bundle author, I want to specify artifact sources (registry image or uploaded binary) per container in a bundle version so that SignalBeam knows where to get the artifacts
  - Acceptance: Create version UI allows specifying source type (registry/upload) per artifact
- As a bundle author, I want to publish a bundle version and have SignalBeam automatically pull and cache all referenced artifacts so that edge devices get fast downloads
  - Acceptance: Publishing triggers async artifact pull, status visible in UI, version not marked "ready" until all artifacts cached

### Edge Device (Agent)
- As an edge agent, I want to download artifacts from SignalBeam's cache via authenticated, time-limited URLs so that I don't need direct registry access
  - Acceptance: Agent receives SAS URLs in desired state, downloads succeed, checksum verified locally
- As an edge agent, I want to verify artifact signatures before running containers so that compromised artifacts are rejected
  - Acceptance: Agent validates signature against tenant's public key, rejects unsigned/invalid artifacts

## 5. Functional Requirements

| ID | Requirement | Priority | Acceptance Criteria |
|----|-------------|----------|---------------------|
| FR-1 | Tenant-level registry credential management (CRUD) | Must | Admin can add/edit/delete registry configs with encrypted credentials |
| FR-2 | Artifact source specification on bundle version containers | Must | Each container in a version specifies source type + source details |
| FR-3 | Registry pull: pull image from private registry and cache in Blob | Must | On version publish, all registry-sourced artifacts are pulled and cached |
| FR-4 | Upload: multipart binary upload to Blob via dashboard | Must | Files up to 2GB uploaded with progress, stored in Blob with metadata |
| FR-5 | Artifact cache in Azure Blob Storage with SAS URL generation | Must | Cached artifacts downloadable via time-limited SAS tokens |
| FR-6 | Edge agent artifact download with checksum verification | Must | Agent downloads from SAS URL, verifies SHA256 before use |
| FR-7 | Artifact signing — opt-in per tenant (sign on cache, verify on agent) | Should | Tenant enables signing → RSA signature on cache, agent verifies with public key. Disabled by default. |
| FR-8 | Artifact status tracking (pending, pulling, cached, failed) | Must | Status queryable per artifact, visible in UI |
| FR-9 | Desired state includes artifact download URLs | Must | Agent receives artifact URLs + checksums in desired state response |
| FR-10 | Frontend: registry credential management page | Must | CRUD UI for tenant registry configs |
| FR-11 | Frontend: artifact source selection in create version dialog | Must | Source type picker (registry/upload) per container |
| FR-12 | Frontend: upload progress indicator | Should | Progress bar during binary upload |
| FR-13 | Retry failed artifact pulls with exponential backoff | Should | Failed pulls retry 3x with backoff, final failure triggers alert |
| FR-14 | Artifact cleanup on version deprecation | Could | Deprecated version artifacts deleted from Blob after retention period |

## 6. Technical Considerations

### Affected Services
- [x] **SignalBeam.Domain** — New entities: `RegistryCredential`, `BundleArtifact`; extend `ContainerSpec` with artifact source; new events
- [x] **BundleOrchestrator** — Artifact pull/cache pipeline, upload endpoint, registry credential CRUD, artifact status tracking
- [x] **SignalBeam.EdgeAgent** — Artifact download from SAS URLs, checksum verification, signature verification
- [ ] SignalBeam.DeviceManager — No direct changes
- [ ] TelemetryProcessor — No direct changes
- [x] **web (frontend)** — Registry config page, artifact source UI in create version, upload with progress

### Data Model Changes

**New entity: `RegistryCredential`**
```
RegistryCredentialId (ValueObject)
TenantId
Name (display name, e.g., "Production ACR")
RegistryUrl (e.g., "myregistry.azurecr.io")
RegistryType (enum: ACR, GHCR, DockerHub, Generic)
Username (encrypted)
Password/Token (encrypted)
CreatedAt, UpdatedAt
```

**New entity: `BundleArtifact`**
```
ArtifactId (ValueObject)
BundleVersionId (FK)
TenantId
Name (container name or binary name)
ArtifactType (enum: ContainerImage, Binary)
SourceType (enum: Registry, Upload)
SourceReference (image ref for registry, original filename for upload)
RegistryCredentialId (nullable FK, for registry sources)
BlobStorageUri
Checksum (SHA256)
Signature (RSA signature of checksum, nullable)
SizeBytes
Status (enum: Pending, Pulling, Cached, Failed, Deleted)
FailureReason (nullable)
CreatedAt, CachedAt
```

**Extend `ContainerSpec`**
- Add `ArtifactSource` value object: `{ SourceType, RegistryCredentialId?, UploadedArtifactId? }`

**Extend `Tenant`**
- Add `SigningKeyPair` (public key stored, private key in Key Vault)

**Migrations required:** Yes — new tables `registry_credentials`, `bundle_artifacts`; alter `container_specs` jsonb

### API Changes

| Endpoint | Method | Change Type | Description |
|----------|--------|-------------|-------------|
| `/api/tenants/{id}/registry-credentials` | GET | New | List tenant registry configs |
| `/api/tenants/{id}/registry-credentials` | POST | New | Add registry credential |
| `/api/tenants/{id}/registry-credentials/{credId}` | PUT | New | Update registry credential |
| `/api/tenants/{id}/registry-credentials/{credId}` | DELETE | New | Delete registry credential |
| `/api/bundles/{id}/versions` | POST | Modified | Accept artifact source per container |
| `/api/bundles/{id}/versions/{ver}/artifacts` | GET | New | List artifacts with status |
| `/api/bundles/{id}/versions/{ver}/artifacts/upload` | POST | New | Multipart binary upload |
| `/api/bundles/{id}/versions/{ver}/artifacts/{artId}/download` | GET | New | Generate SAS download URL |
| `/api/devices/{id}/desired-state` | GET | Modified | Include artifact download URLs + checksums |

### NATS Subjects

```
signalbeam.bundles.artifacts.<bundleId>.<version>    # Artifact status changes (JetStream)
signalbeam.bundles.artifacts.pull-request             # Internal: trigger artifact pull worker
```

### External Integrations
- **Azure Blob Storage** — Artifact cache (existing, extend with new container `bundle-artifacts`)
- **Azure Key Vault** — Store tenant signing private keys and encrypted registry credentials
- **Container Registries** — Pull images via Docker Registry HTTP API v2 (ACR, GHCR, DockerHub, generic)

### Performance Considerations
- Artifact pull is async — don't block version creation on pull completion
- SAS tokens should have short TTL (15 minutes) to limit exposure
- Large file uploads need chunked multipart (Azure Blob supports block blobs up to 190TB)
- 500 concurrent device pulls against Blob Storage — Azure handles this natively, no custom throttling needed
- Consider CDN in front of Blob for geographically distributed fleets (future)

### Security Considerations
- Registry credentials encrypted at rest (AES-256 via Key Vault)
- Registry credentials never sent to edge agents — SignalBeam pulls on their behalf
- SAS tokens are device-scoped and time-limited
- Artifact signatures prevent tampering between cache and device
- Binary uploads scanned for max size limit (2GB), content-type validation
- Tenant isolation: artifacts stored under `{tenantId}/` prefix, cross-tenant access impossible

## 7. Dependencies

### Upstream
- Azure Blob Storage (existing)
- Azure Key Vault (new dependency for credential encryption + signing keys)
- Docker Registry HTTP API v2 (for pulling from registries)

### Downstream
- Rollout tracking — rollouts depend on artifacts being cached before starting
- Device reconciliation — agents need artifact URLs in desired state

### External
- Container registries (ACR, GHCR, DockerHub) — availability affects pull success
- Azure Blob Storage — availability affects downloads

## 8. Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Registry pull fails (auth, network, rate limit) | High | Medium | Retry with backoff, surface clear error in UI, don't block other artifacts |
| Large artifacts slow to cache (multi-GB images) | Medium | Medium | Async pull with progress tracking, stream directly to Blob (no temp disk) |
| SAS token leaked by compromised device | Low | High | Short TTL (15min), device-scoped tokens, audit log on generation |
| Registry credential compromise | Low | Critical | Encrypt at rest via Key Vault, audit access, rotate support |
| Blob Storage costs with many large artifacts | Medium | Medium | Artifact cleanup on deprecation, storage metrics in dashboard, retention policies |
| Docker Registry API v2 inconsistencies across providers | Medium | Medium | Abstract behind interface, test against ACR + GHCR + DockerHub |

## 9. Out of Scope

- **Image layer deduplication** — storing shared layers once across versions (optimization for later)
- **Delta updates** — sending only changed layers to devices (significant complexity, future)
- **Air-gapped/USB sideload** — requires offline artifact transfer mechanism
- **Build from Dockerfile** — users build externally, we distribute
- **Multi-arch resolution** — agent specifies exact image, no manifest list resolution
- **CDN distribution** — Azure Blob is sufficient for medium scale
- **Artifact vulnerability scanning** — integrate with Trivy/Grype later
- **Registry webhook triggers** — auto-pull new tags when pushed to registry (future automation)

## 10. Resolved Questions

| # | Question | Decision | Rationale |
|---|----------|----------|-----------|
| 1 | Credential encryption: Key Vault or ASP.NET Data Protection? | **Azure Key Vault** | Already using Azure Blob + Managed Identity. Key Vault provides HSM-backed encryption, managed rotation, and audit logging. ~10ms latency acceptable since credentials are rarely accessed. |
| 2 | Registry pull strategy: tarball or individual layers? | **Tarball** | Pull manifest → pull each layer → bundle as tar.gz → store in Blob. Simpler for MVP. Layer deduplication is a future optimization when scale demands it. |
| 3 | Edge agent apply mechanism? | **`docker load`** | Agent downloads tarball from SAS URL → `docker load` → `docker run`. Simple, works offline, no local registry needed. Loses Docker layer cache between versions but acceptable for medium scale. |
| 4 | Maximum artifact size? | **2GB** | Covers most edge workloads (typical containers 50-500MB). Azure Blob supports up to 190TB so this is a policy limit, easily raised. Frontend uses chunked multipart upload. |
| 5 | Artifact signing mandatory? | **Opt-in for MVP** | Ship with SHA256 checksum verification mandatory on every download. Signing (RSA) is opt-in per tenant. Reduces critical path complexity while still ensuring integrity. |
| 6 | Registry credential rotation? | **Manual only for MVP** | Admin updates credentials through the UI when they rotate in the source registry. Matches how most teams manage registry creds. Auto-rotate is a future enhancement. |

## 11. Task Breakdown Hints

### Domain Layer
- New value objects: `RegistryCredentialId`, `ArtifactId`, `ArtifactSource`
- New entities: `RegistryCredential`, `BundleArtifact`
- New events: `ArtifactPullRequestedEvent`, `ArtifactCachedEvent`, `ArtifactPullFailedEvent`, `RegistryCredentialCreatedEvent`
- Extend `ContainerSpec` with `ArtifactSource`
- New enums: `ArtifactType`, `ArtifactSourceType`, `ArtifactStatus`, `RegistryType`

### Application Layer (BundleOrchestrator)
- Commands: `CreateRegistryCredential`, `UpdateRegistryCredential`, `DeleteRegistryCredential`, `UploadBinaryArtifact`, `RequestArtifactPull`
- Queries: `GetRegistryCredentials`, `GetBundleArtifacts`, `GetArtifactDownloadUrl`
- Event handlers: `ArtifactPullRequestedHandler` (triggers async pull), `ArtifactCachedHandler` (updates version readiness)
- New service interface: `IRegistryClient` (pull images from registries)
- Extend `IBundleStorageService` with artifact blob operations

### Infrastructure Layer
- `RegistryClient` — Docker Registry HTTP API v2 implementation
- `ArtifactStorageService` — Azure Blob operations for artifacts (separate container from manifests)
- EF Core configurations for new entities
- Credential encryption service (Key Vault or Data Protection)
- Migrations: `AddRegistryCredentials`, `AddBundleArtifacts`, `ExtendContainerSpec`

### Endpoints (BundleOrchestrator.Host)
- Registry credential CRUD endpoints
- Artifact list/download endpoints
- Binary upload endpoint (multipart)
- Modify create version endpoint to accept artifact sources
- Modify desired state endpoint to include artifact URLs

### EdgeAgent
- Artifact downloader service (HTTP download from SAS URL to temp file)
- SHA256 checksum verification after download (mandatory)
- RSA signature verification after download (opt-in, checks tenant config)
- `docker load` from downloaded tarball for container images
- Binary artifact placement to configurable target path

### Frontend
- Registry credentials management page (new feature module)
- Artifact source selector in create version dialog
- File upload component with progress
- Artifact status display on bundle version detail
- Download URL copy button for debugging

### Tests
- Unit: RegistryCredential entity validation, ArtifactSource value object, checksum calculation
- Unit: Artifact pull command handler, upload command handler
- Integration: Registry pull from mock registry (WireMock), blob storage operations (Testcontainers + Azurite)
- Integration: End-to-end artifact delivery (upload → cache → SAS URL → download → verify)
- EdgeAgent: Download + verify flow with mock SAS URLs

---

## Appendix: Codebase Analysis

### Current Bundle Architecture
- **AppBundle** (`src/Shared/SignalBeam.Domain/Entities/AppBundle.cs`): Name, description, latest version reference
- **AppBundleVersion** (`src/Shared/SignalBeam.Domain/Entities/AppBundleVersion.cs`): Version, containers, blob metadata (`BlobStorageUri`, `Checksum`, `SizeBytes`), status (Draft/Published/Deprecated)
- **ContainerSpec** (`src/Shared/SignalBeam.Domain/ValueObjects/ContainerSpec.cs`): Name, Image (string), env, ports, volumes — no artifact source metadata
- **BundleDefinition** (`src/BundleOrchestrator/.../Models/BundleDefinition.cs`): JSON model with containers, no registry credentials

### Existing Storage Infrastructure
- **IBundleStorageService** (`src/BundleOrchestrator/.../Storage/IBundleStorageService.cs`): Upload/download manifests, SAS URL generation, checksum validation
- **BundleStorageService** (`src/BundleOrchestrator/.../Storage/BundleStorageService.cs`): Azure Blob implementation, path `{tenantId}/{bundleId}/versions/{version}.json`, SAS tokens, SHA256
- **Packages**: `Azure.Storage.Blobs` 12.22.2, `Azure.Identity` 1.13.1 — already available

### Edge Agent Flow
- **ReconciliationService** (`src/EdgeAgent/.../Services/ReconciliationService.cs`): Polls desired state → reconciles containers → reports status
- **HttpCloudClient** (`src/EdgeAgent/.../Cloud/HttpCloudClient.cs`): `GET /api/devices/{id}/desired-state` → receives bundle definition JSON
- **Gap**: Agent receives image references but has no download URLs or credentials

### Integration Points
- NATS: `signalbeam.bundles.assignments.<deviceId>` (existing), needs new artifact subjects
- Blob Storage: Existing client in BundleOrchestrator DI, extend for artifact container
- Frontend: `web/src/features/bundles/` has full CRUD UI, `create-version-dialog.tsx` needs artifact source fields
- No multipart upload exists anywhere — new capability
