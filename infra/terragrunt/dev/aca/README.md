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
| `zitadel-init` | Zitadel DB init — eventstore schema + base tables (Container App **Job**) | — | one-shot | Runs `zitadel init`; must complete before `zitadel-setup` |
| `zitadel-setup` | Zitadel migrations + first-instance bootstrap (Container App **Job**) | — | one-shot | Runs `zitadel setup` to completion; keeps migrations out of the service |
| `zitadel` | Zitadel OIDC provider | **external HTTPS** | **1** (always-on) | Runs `zitadel start` (no migrations); OIDC for the dashboard + IdentityManager; reuses the shared Postgres (#389) |

**Omitted for the lean dogfood:** Valkey (confirmed unused — caching is in-process
`IMemoryCache`), ACR (images come from private GHCR). See issue #375 for the full
cost rationale. **Zitadel** was originally omitted but is now deployed (#389) so
IdentityManager's JWT/OIDC endpoints and the dashboard login work in the cloud —
it adds ~$10–14/mo (one always-on small container; the `zitadel` database reuses
the existing Postgres server at no extra DB cost).

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
6. **Allowlist your IP on the Key Vault firewall.** The vault (`sb-kv-dev-neu`) is
   `Deny`-by-default with `bypass = AzureServices`, so the Azure CLI is *not* a
   trusted caller — applying the `key-vault`/`secrets` units from your machine
   fails with `403 ForbiddenByFirewall` until your public IP is allowed:

   ```bash
   # the 403 error prints your "Client address" — use that IP
   az keyvault network-rule add --name sb-kv-dev-neu --ip-address <your-ip>
   az keyvault network-rule list --name sb-kv-dev-neu -o jsonc   # verify (~30–60s to propagate)
   ```

   The KV module has `ignore_changes` on `ip_rules`, so this manual rule persists
   and a later `apply` won't revert it. Re-add if your ISP IP changes. The runtime
   path is unaffected (container apps reach KV via managed identity, not your IP).
   **Don't Ctrl-C a terragrunt run** mid-apply on this stack — it can orphan the
   state lock.

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

# ACA stack — includes the Static Web Apps unit (aca/static-web-app). Terragrunt
# applies it before apigateway, which reads its hostname to allow-list the
# dashboard origin for CORS.
terragrunt run --all apply --working-dir ./aca
```

> The `static-web-app` unit lives inside `./aca`, so `terragrunt run --all` walks
> it and orders it ahead of `apigateway` (which depends on its hostname for CORS)
> automatically — no separate manual apply step. See
> [Web dashboard](#web-dashboard-azure-static-web-apps) below.

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
a permanent `https://<name>.azurestaticapps.net` URL (issue #383). The SWA unit
lives inside the ACA group at `infra/terragrunt/dev/aca/static-web-app` (so it is
applied and ordered with the rest of the stack) but is **pinned to West Europe**
because the Free tier is not offered in northeurope.

```bash
# Applied as part of `run --all ./aca`, or on its own:
terragrunt apply --working-dir ./aca/static-web-app   # from infra/terragrunt/dev

# Grab the hostname and deployment token
terragrunt output --working-dir ./aca/static-web-app default_host_name
terragrunt output --raw --working-dir ./aca/static-web-app api_key
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

The dashboard is configured for **OIDC via Zitadel**. Zitadel is now deployed as
part of this stack (#389) — see [Identity provider](#identity-provider-zitadel--oidc)
below for the one-time bootstrap that creates the SPA client and registers the SWA
redirect URIs (`https://<swa-host>/callback`, post-logout `https://<swa-host>`).

## Identity provider (Zitadel / OIDC)

The `zitadel` unit runs [Zitadel](https://zitadel.com) `v2.66.3` as an always-on,
externally-reachable container app — the OIDC authority for both the dashboard and
IdentityManager (#389). It uses the dedicated `zitadel` database on the shared
Postgres server and these Key Vault secrets (all provisioned automatically):
`zitadel-master-key`, `postgresql-admin-password` (as the DB password), and
`zitadel-admin-password` (the first-instance admin login).

**Topology:** external ingress (`transport = http2`). The browser and backend
services talk to Zitadel **directly** at its own FQDN — not through the gateway —
which avoids proxying Zitadel's gRPC APIs through YARP. `ZITADEL_EXTERNALDOMAIN` is
set to that FQDN so issuer/discovery URLs match what the browser uses.

**Bootstrap is split into two one-shot jobs.** Zitadel's lifecycle is layered —
`init` (create eventstore schema + base tables + app role) → `setup` (migrations +
first instance) → `start` (serve). The old `start-from-init` ran all three in one
process and raced itself on the `03_default_instance` migration. Here:

- `zitadel-init` Container App Job runs `zitadel init`
- `zitadel-setup` Container App Job runs `zitadel setup` (depends on init)
- the `zitadel` service runs `zitadel start` with **no** migrations

Because the service never migrates, overlapping replicas during an ACA rolling
revision can never race the migration — so the service keeps a normal readiness
probe and the old revision retires cleanly. Both jobs are `parallelism = 1` and
idempotent, so re-running them on an already-bootstrapped DB is a no-op.

The units also set **hostname-based machine identification**
(`ZITADEL_MACHINE_IDENTIFICATION_HOSTNAME_ENABLED=true`, Private-IP + webhook
disabled). Zitadel's defaults (Private IP + GCP metadata) both fail on ACA and
panic with *"none of the enabled methods for identifying the machine succeeded"*;
ACA gives each replica a unique hostname (like a K8s pod), which works.

```bash
# FQDN + first-instance admin password
terragrunt output --working-dir ./aca/zitadel fqdn
az keyvault secret show --vault-name sb-kv-dev-neu --name zitadel-admin-password --query value -o tsv
# Admin console: https://<zitadel-fqdn>  (user: admin, pw: the secret above)
```

### Prerequisite — grant DB privileges (run once, before the init job)

Terraform pre-creates the `zitadel` database owned by `azure_pg_admin`, so
`pgadmin` lacks CREATE on it and `zitadel init` (CREATE SCHEMA/ROLE) fails. The
Postgres server is **private (VNet-only, no public endpoint)** — firewall rules
are rejected and you cannot reach it from a workstation. Run the grant from an
in-VNet one-shot job instead (admin password pulled from Key Vault via the
workload managed identity — nothing sensitive on your machine, no KV firewall
change). Save as `dbgrant-job.yaml`:

```yaml
location: northeurope
identity:
  type: UserAssigned
  userAssignedIdentities:
    /subscriptions/<sub>/resourcegroups/sb-rg-dev-neu/providers/Microsoft.ManagedIdentity/userAssignedIdentities/sb-id-workload-dev-neu: {}
properties:
  environmentId: /subscriptions/<sub>/resourceGroups/sb-rg-dev-neu/providers/Microsoft.App/managedEnvironments/sb-cae-dev-neu
  configuration:
    triggerType: Manual
    replicaTimeout: 300
    replicaRetryLimit: 0
    manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }
    secrets:
      - name: pgpw
        keyVaultUrl: https://sb-kv-dev-neu.vault.azure.net/secrets/postgresql-admin-password
        identity: /subscriptions/<sub>/resourcegroups/sb-rg-dev-neu/providers/Microsoft.ManagedIdentity/userAssignedIdentities/sb-id-workload-dev-neu
  template:
    containers:
      - name: dbgrant
        image: postgres:16-alpine
        resources: { cpu: 0.25, memory: 0.5Gi }
        env: [{ name: PGPASSWORD, secretRef: pgpw }]
        command: ["/bin/sh"]
        args:
          - -c
          - >-
            psql "host=sb-psql-dev-neu.postgres.database.azure.com user=pgadmin dbname=postgres sslmode=require"
            -c 'ALTER DATABASE zitadel OWNER TO pgadmin;'
            -c 'GRANT ALL PRIVILEGES ON DATABASE zitadel TO pgadmin;'
```

```bash
RG=sb-rg-dev-neu
az containerapp job create --name sb-caj-dbgrant-dev -g $RG --yaml dbgrant-job.yaml
az containerapp job start  --name sb-caj-dbgrant-dev -g $RG   # logs should show ALTER DATABASE / GRANT
az containerapp job delete --name sb-caj-dbgrant-dev -g $RG --yes
```

> Pass `command`/`args` via YAML, not `az ... --args "-c"` — the CLI parser
> rejects values that start with `-`.

### Deploy order — init → setup → service

`terragrunt run --all apply` creates the two jobs and the service in dependency
order (`zitadel` → `zitadel-setup` → `zitadel-init`). The jobs are **manually
triggered**, so after apply, run them in order, then restart the service:

```bash
RG=sb-rg-dev-neu

# 1) init — eventstore schema + base tables (wait for Succeeded)
az containerapp job start --name sb-caj-zitadel-init-dev -g $RG
az containerapp job execution list --name sb-caj-zitadel-init-dev -g $RG --query "[0].properties.status" -o tsv

# 2) setup — migrations + first instance (wait for Succeeded)
az containerapp job start --name sb-caj-zitadel-setup-dev -g $RG
az containerapp job execution list --name sb-caj-zitadel-setup-dev -g $RG --query "[0].properties.status" -o tsv

# 3) restart the service so `zitadel start` runs against the migrated DB
az containerapp revision restart --name sb-ca-zitadel-dev -g $RG \
  --revision "$(az containerapp show -n sb-ca-zitadel-dev -g $RG --query properties.latestRevisionName -o tsv)"

# verify
curl -sf "https://$(terragrunt output --working-dir ./aca/zitadel fqdn | tr -d '"')/debug/ready" && echo OK
```

> **Note on job logs:** Zitadel writes to stderr, which `az containerapp job logs
> show` does not surface — it appears empty. To see real errors, query Log
> Analytics: `ContainerAppConsoleLogs_CL | where ContainerName_s ==
> 'sb-caj-zitadel-setup-dev'`.

Re-run init + setup before any future Zitadel image upgrade that ships new
migrations; both are idempotent on an already-bootstrapped DB.

### One-time bootstrap (project + SPA client)

The `zitadel setup` job + the `ZITADEL_FIRSTINSTANCE_*` env vars create the
instance and admin, but the **SignalBeam project + OIDC SPA app** must be created
once. Use the `SignalBeam.ZitadelSetup` tool (now parameterised for the deployed
host) — create a service user + PAT in the Zitadel console first, then:

```bash
ZITADEL_URL=https://<zitadel-fqdn> \
ZITADEL_PAT=<pat-from-console> \
WEB_BASE_URL=https://<swa-host> \
OIDC_AUTHORITY=https://<zitadel-fqdn> \
BACKEND_REQUIRE_HTTPS=true \
CONFIG_OUTPUT_PATH=./zitadel-config.json \
dotnet run --project src/SignalBeam.ZitadelSetup
# prints Project ID and Client ID; registers the SWA redirect URIs on the SPA app
```

`CONFIG_OUTPUT_PATH` is overridden because the default (`/app/config/...`) only
exists inside the Aspire container. If you wire this into CI, set `ZITADEL_PAT` as
a **secret**, never a plain variable.

(Or create the project/app manually in the console — see `docs/zitadel-setup.md`.)

### After bootstrap — wire the IDs (out-of-band, like the GHCR PAT)

The project/client IDs only exist after bootstrap, so set them on the consumers:

```bash
# Dashboard (GitHub repo Variables for the SWA build — see the table above)
VITE_ZITADEL_AUTHORITY=https://<zitadel-fqdn>
VITE_ZITADEL_CLIENT_ID=<client-id>
VITE_ZITADEL_PROJECT_ID=<project-id>

# IdentityManager — set the audience to enable JWT audience validation
az containerapp update -n sb-ca-identitymanager-dev -g <rg> \
  --set-env-vars Authentication__Jwt__Audience=<project-id>
```

IdentityManager's `Authority`/`RequireHttpsMetadata` are already wired to the
deployed Zitadel by the `identitymanager` unit; only the audience is post-bootstrap.
Until it is set, `ValidateAudience` stays off (issuer/lifetime/signature are still
enforced) rather than rejecting every token.

## Cost controls

- All stateless apps scale to zero; **NATS and Zitadel run 24/7** (Zitadel adds
  ~$10–14/mo — it cannot scale to zero without breaking token/JWKS validation).
- `terragrunt run --all destroy --working-dir ./aca` between dogfood sessions
  drops cost to storage-only (~$3–5/mo). Postgres can also be stopped overnight.
- To run the lean stack **without** OIDC and avoid the Zitadel cost, exclude the
  `zitadel` unit (`--queue-exclude-dir ./aca/zitadel`) and leave the dashboard /
  IdentityManager audience unset.
