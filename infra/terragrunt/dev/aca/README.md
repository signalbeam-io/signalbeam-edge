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

# Web dashboard host (SWA) — apply before the ACA stack: the apigateway unit
# reads its hostname to allow-list the dashboard origin for CORS.
terragrunt apply --working-dir ./static-web-app

# ACA stack
terragrunt run --all apply --working-dir ./aca
```

> The `apigateway` unit depends on `static-web-app` for its CORS origin. Apply
> `static-web-app` first (one-time) so `terragrunt run --all ./aca` can resolve
> the hostname; otherwise the gateway apply fails on the missing dependency
> output. See [Web dashboard](#web-dashboard-azure-static-web-apps) below.

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

## Web dashboard (Azure Static Web Apps)

The React dashboard (`web/`) is hosted on **Azure Static Web Apps (Free)**, giving
a permanent `https://<name>.azurestaticapps.net` URL (issue #383). SWA is a
separate unit from the ACA stack — it lives in `infra/terragrunt/dev/static-web-app`
and is **pinned to West Europe** because the Free tier is not offered in
northeurope.

```bash
# Provision the Static Web App (one-time)
terragrunt apply --working-dir ./static-web-app   # from infra/terragrunt/dev

# Grab the hostname and deployment token
terragrunt output --working-dir ./static-web-app default_host_name
terragrunt output --raw --working-dir ./static-web-app api_key
```

### Wire up CI deployment

The `deploy-web-swa.yml` workflow builds the Vite app and deploys it to SWA on
every push to `main` that touches `web/`. Configure these in GitHub
(**Settings → Secrets and variables → Actions**):

| Kind | Name | Value |
|------|------|-------|
| Secret | `AZURE_STATIC_WEB_APPS_API_TOKEN` | the `api_key` output above |
| Variable | `VITE_API_URL` | `https://<apigateway-fqdn>` (the ACA gateway) |
| Variable | `VITE_AUTH_MODE` | `zitadel` |
| Variable | `VITE_ZITADEL_AUTHORITY` | Zitadel instance URL |
| Variable | `VITE_ZITADEL_CLIENT_ID` | SPA client ID |
| Variable | `VITE_ZITADEL_PROJECT_ID` | Zitadel project ID |

### CORS

The gateway allow-lists the SWA origin automatically: the `apigateway` unit reads
`default_host_name` from the `static-web-app` unit and sets
`Cors__AllowedOrigins__0=https://<swa-host>`. Re-apply `apigateway` (or let the
next revision roll) after the SWA exists so the origin takes effect.

### Auth prerequisite (OIDC / Zitadel)

The dashboard is configured for **OIDC via Zitadel**, but Zitadel is **not** part
of the lean ACA stack (omitted per #375). Until an identity provider is
provisioned (tracked by the IdentityManager auth issue), login will not complete.
When Zitadel is available, register the SWA URLs in the SPA client:

- Redirect URI: `https://<swa-host>/callback`
- Post-logout redirect URI: `https://<swa-host>`

## Cost controls

- All stateless apps scale to zero; only NATS runs 24/7.
- `terragrunt run --all destroy --working-dir ./aca` between dogfood sessions
  drops cost to storage-only (~$3–5/mo). Postgres can also be stopped overnight.
