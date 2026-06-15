# SignalBeam control plane on Azure Container Apps (dev/dogfood)

Deploys the SignalBeam backend to **Azure Container Apps (ACA)** as a low-cost,
scale-to-zero alternative to the AKS stack. Targets **~$20/mo** for a single
dogfood device (issue #375). Unblocks Raspberry Pi onboarding (#371) by exposing
a public HTTPS endpoint the EdgeAgent can reach via `--cloud-url`.

## What gets deployed

| Unit | Resource | Ingress | Replicas | Notes |
|------|----------|---------|----------|-------|
| `environment` | Container Apps environment (Consumption) + Azure Files for NATS | — | — | VNet-injected via `snet-aca` (/27), public LB enabled |
| `secrets` | Key Vault secrets (DB connection string, GHCR PAT placeholder) | — | — | Consumed as Key Vault references |
| `nats` | NATS + JetStream | internal TCP :4222 | **1** (always-on) | JetStream persisted on Azure Files |
| `apigateway` | YARP gateway | **external HTTPS** | 0→1 | The only public app; EdgeAgent talks here |
| `devicemanager` | DeviceManager | internal HTTPS | 0→1 | scale-to-zero |
| `bundleorchestrator` | BundleOrchestrator | internal HTTPS | 0→1 | scale-to-zero |
| `telemetryprocessor` | TelemetryProcessor | internal HTTPS | 0→1 | scale-to-zero |
| `identitymanager` | IdentityManager | internal HTTPS | 0→1 | scale-to-zero |

**Omitted for the lean dogfood:** Valkey (confirmed unused — caching is in-process
`IMemoryCache`), Zitadel (device flows use API keys, not OIDC), ACR (images come
from private GHCR). See issue #375 for the full cost rationale.

## Security posture

- **Secrets are Key Vault references**, resolved at runtime by the shared
  user-assigned managed identity (`Key Vault Secrets User`). Secret *values* are
  never written into the container app definitions or Terraform state — only the
  versionless Key Vault secret URIs are.
- **Private GHCR** access uses a PAT held in Key Vault (`ghcr-pat`) and wired as
  an ACA registry credential.
- **Postgres** stays private (VNet-only, TLS-required); the ACA subnet is granted
  5432 access via an explicit NSG rule and added to the Key Vault network ACL.
- **mTLS is deferred** for the dogfood: the device registration handshake uses API
  keys (per #375 acceptance criteria). Device-certificate mTLS (PR #374) would
  require client-cert passthrough through the external ApiGateway and is a
  follow-up, not part of this deployment.
- The **NATS Azure Files storage account denies public access** and is restricted
  to the ACA subnet via a `Microsoft.Storage` service endpoint. SMB mounts require
  the account key (`shared_access_key_enabled = true`) — that is an ACA platform
  constraint, mitigated by the network lock-down.

### Terraform state contains sensitive material

Two values that ACA + Terraform cannot keep out of state:

- the Postgres connection string (with password) in the `dev/aca/secrets` state blob, and
- the NATS storage account key in the `dev/aca/environment` state blob (ACA needs
  it at share-registration time).

The remote state lives in the `sbtfstatedevweu` storage account. Restrict read
access to that account to the deploy identity only (Storage Blob Data Reader/
Contributor scoped to the CI principal), and do not grant broad subscription
Reader to it. The Postgres password already exists in the `key-vault` state, so no
*new* class of secret is exposed — but the access controls on the state account
are the real boundary protecting these values. The **GHCR PAT is the exception**:
it is set out-of-band (below) and never touches state.

## Prerequisites

1. Azure subscription + `az login`.
2. Terraform state backend bootstrapped (`infra/terragrunt/bootstrap.sh`).
3. Set the real subscription id in `infra/terragrunt/dev/env.hcl`.
4. Container images published to `ghcr.io/signalbeam-io/{service}:latest`
   (`apigateway`, `devicemanager`, `bundleorchestrator`, `telemetryprocessor`,
   `identitymanager`).
5. A GitHub PAT with `read:packages` for pulling private GHCR images.

## Deploy

Terragrunt resolves the dependency order automatically. The base Azure units
(`resource-group`, `networking`, `managed-identity`, `monitoring`, `key-vault`,
`postgresql`) must exist first; then apply the ACA stack:

```bash
cd infra/terragrunt/dev

# Base platform (if not already applied)
terragrunt run --all apply --queue-include-dir ./resource-group --queue-include-dir ./networking \
  --queue-include-dir ./managed-identity --queue-include-dir ./monitoring \
  --queue-include-dir ./key-vault --queue-include-dir ./postgresql

# ACA stack
terragrunt run --all apply --working-dir ./aca
```

### Set the GHCR PAT (out-of-band — never in Terraform state)

The `secrets` unit creates a `ghcr-pat` placeholder with `ignore_changes` on its
value. Set the real PAT once after the secret exists:

```bash
az keyvault secret set --vault-name sb-kv-dev-weu --name ghcr-pat --value <YOUR_GHCR_PAT>
```

Restart the apps (or apply) so the new revision picks up the value.

### Run database migrations

Point EF Core at the deployed Postgres (private — run from a VNet-connected host,
or temporarily allow your IP). The connection string is in Key Vault as
`db-connection-signalbeam`.

## Get the agent's `--cloud-url`

```bash
terragrunt output --working-dir ./aca/apigateway fqdn
# -> sb-ca-apigateway-dev.<hash>.westeurope.azurecontainerapps.io
```

The EdgeAgent uses `--cloud-url https://<that-fqdn>`. Verify the public
registration handshake is reachable:

```bash
curl -i https://<apigateway-fqdn>/api/devices   # routed by YARP to DeviceManager
```

## Cost controls

- All stateless apps scale to zero; only NATS runs 24/7.
- `terragrunt run --all destroy --working-dir ./aca` between dogfood sessions
  drops cost to storage-only (~$3–5/mo). Postgres can also be stopped overnight.
