# Cookbook: Deploying a stateful app with DB migrations on Azure Container Apps

A reusable recipe for running a stateful service that needs **one-shot DB
bootstrap/migration before it can serve** (e.g. Zitadel, Keycloak, or any app
with a `migrate` step) on the SignalBeam ACA stack — without the migration-race
deadlocks you hit when the service migrates itself.

**Worked example:** Zitadel (`infra/terragrunt/dev/aca/zitadel*`). Substitute your
own service throughout.

> Why this pattern: if the long-running service runs migrations at startup, two
> overlapping replicas during an ACA rolling revision race the same migration and
> deadlock. Split migration into one-shot **jobs** and let the service do nothing
> but serve.

---

## 0. Variables (edit per env / service)

These follow the repo naming convention `{project}-{kind}-{env}-{loc}`. For a new
env, only `ENV` / `LOC` / `SUB` change.

```bash
SUB=3fee91b5-7993-40b1-93ba-b76893ff03db   # az subscription id
ENV=dev
LOC=northeurope ; LOCSHORT=neu
PROJECT=sb
SVC=zitadel                                 # your service slug

RG=$PROJECT-rg-$ENV-$LOCSHORT
CAE=$PROJECT-cae-$ENV-$LOCSHORT             # container apps environment
KV=$PROJECT-kv-$ENV-$LOCSHORT
PG=$PROJECT-psql-$ENV-$LOCSHORT
MI=$PROJECT-id-workload-$ENV-$LOCSHORT      # workload managed identity (has KV Secrets User)
MI_ID=$(az identity show -g $RG -n $MI --query id -o tsv)
```

---

## 1. The IaC shape

Three pieces, in dependency order — the canonical **init → setup → start**:

| Unit | Module | Command | Trigger | Purpose |
|------|--------|---------|---------|---------|
| `{svc}-init`  | `container-app-job` | `init`  | Manual one-shot | create schema / base tables / DB role |
| `{svc}-setup` | `container-app-job` | `setup` | Manual one-shot | run migrations + first-time data; depends on init |
| `{svc}`       | `container-app`     | `start` | always-on (min 1) | serve only — **no migrations** |

Key module choices (see `infra/terraform/modules/container-app-job`):

- **`container-app-job`** wraps `azurerm_container_app_job` — `triggerType: Manual`,
  `parallelism = 1`, `replicaCompletionCount = 1` (one execution = one replica, so
  the job can never race itself). Jobs require an explicit `location` (apps don't).
- The **service** runs `start` only, keeps a normal `readiness_probe_path`
  (e.g. `/debug/ready`), and `min_replicas = 1` for a stateful app. Because it
  doesn't migrate, overlapping revisions during a rollout are safe.
- Secrets are Key Vault references resolved at runtime by the workload identity —
  values never enter Terraform state.

If your image is **distroless** (no `/bin/sh`), you cannot chain `init && setup` in
one container — that is exactly why these are three separate one-shots.

---

## 2. Deploy

```bash
cd infra/terragrunt/dev
terragrunt run --all apply --working-dir ./aca      # provisions jobs + service in DAG order
```

`apply` **creates** the jobs but does **not** run them (they're manual-trigger),
and Terraform cannot reach the private Postgres to grant DB privileges. So the
bootstrap below is required after apply. The service will crash-loop (harmlessly)
until the jobs have run.

### 2a. Grant DB privileges (in-VNet, one-shot)

Postgres here is **VNet-private** (`public_network_access_enabled = false`):
`az postgres ... firewall-rule` is rejected and your workstation cannot reach it.
Terraform pre-creates the DB owned by `azure_pg_admin`, so the admin role
(`pgadmin`) lacks `CREATE` and `init` fails. Grant it from a throwaway job that
runs inside the environment's VNet, pulling the admin password from Key Vault via
the managed identity.

`dbgrant-job.yaml` (replace `<sub>`, `$SVC` DB name, server host):

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
az containerapp job create --name $PROJECT-caj-dbgrant-$ENV -g $RG --yaml dbgrant-job.yaml
az containerapp job start  --name $PROJECT-caj-dbgrant-$ENV -g $RG     # logs: ALTER DATABASE / GRANT
az containerapp job delete --name $PROJECT-caj-dbgrant-$ENV -g $RG --yes
```

> **Gotcha — `az ... --args "-c"` fails.** The CLI parser treats any value
> starting with `-` as a flag ("unrecognized arguments: -c ..."). Always pass
> `command`/`args` as **YAML lists** (as above), or use `az containerapp job
> update --args` only for non-dash values.

### 2b. Run init → setup → restart

```bash
# init (wait for Succeeded)
az containerapp job start --name $PROJECT-caj-$SVC-init-$ENV -g $RG
az containerapp job execution list --name $PROJECT-caj-$SVC-init-$ENV -g $RG --query "[0].properties.status" -o tsv

# setup (wait for Succeeded)
az containerapp job start --name $PROJECT-caj-$SVC-setup-$ENV -g $RG
az containerapp job execution list --name $PROJECT-caj-$SVC-setup-$ENV -g $RG --query "[0].properties.status" -o tsv

# restart the service so `start` picks up the migrated DB
az containerapp revision restart --name $PROJECT-ca-$SVC-$ENV -g $RG \
  --revision "$(az containerapp show -n $PROJECT-ca-$SVC-$ENV -g $RG --query properties.latestRevisionName -o tsv)"
```

### 2c. Verify

```bash
FQDN=$(terragrunt output --working-dir ./aca/$SVC fqdn | tr -d '"')
curl -sf "https://$FQDN/debug/ready" && echo OK        # adjust health path per app
```

---

## 3. Reading job logs (important)

`az containerapp job logs show` only surfaces **stdout**. Many apps (Zitadel
included) log to **stderr**, so the command looks empty even on failure. Read the
real logs from Log Analytics:

```bash
WSID=$(az containerapp env show -n $CAE -g $RG \
  --query "properties.appLogsConfiguration.logAnalyticsConfiguration.customerId" -o tsv)

az monitor log-analytics query -w "$WSID" --analytics-query \
  "ContainerAppConsoleLogs_CL | where TimeGenerated > ago(30m)
   | where ContainerName_s == '$PROJECT-caj-$SVC-setup-$ENV'
   | project TimeGenerated, Log_s | order by TimeGenerated desc | take 60" -o table
```

---

## 4. Recovering a stuck migration

If a previous combined `start-from-init` (or any interrupted migrate) crashed
mid-migration, it can leave a "started but not done" lock and the next run **hangs
forever**. Symptoms: setup execution stays `Running` with no progress.

1. Stop the stuck execution: `az containerapp job stop -n <job> -g $RG --job-execution-name <exec>`.
2. Inspect the eventstore (run a read-only psql job): look for `system.migration.started`
   rows in `eventstore.events2` with no matching `done`.
3. **Cleanest fix when the DB has no real data:** drop & recreate it, matching
   Terraform's charset, then re-run init → setup:
   ```sql
   DROP DATABASE IF EXISTS <db> WITH (FORCE);
   CREATE DATABASE <db> OWNER pgadmin TEMPLATE template0
     ENCODING 'UTF8' LC_COLLATE 'en_US.utf8' LC_CTYPE 'en_US.utf8';
   ```
   (Recreating with the same charset/collation avoids Terraform drift.)
   Deleting just the `started` markers can leave partial DDL that then fails — a
   clean DB is more reliable when there's nothing to lose.

---

## 5. App-specific gotchas

These are per-app; the Zitadel ones are recorded here because they cost real time:

- **Machine-ID panic (Zitadel):** *"none of the enabled methods for identifying the
  machine succeeded"*. Zitadel's defaults (Private IP + GCP metadata webhook) both
  fail on ACA. Set on the service **and** jobs:
  ```
  ZITADEL_MACHINE_IDENTIFICATION_HOSTNAME_ENABLED=true
  ZITADEL_MACHINE_IDENTIFICATION_PRIVATEIP_ENABLED=false
  ZITADEL_MACHINE_IDENTIFICATION_WEBHOOK_ENABLED=false
  ```
  ACA gives each replica a unique hostname (like a K8s pod), so hostname works.
- **External addressing must match the browser host** or OIDC/issuer validation
  breaks — set `ZITADEL_EXTERNALDOMAIN` to the predictable ACA FQDN
  `<app-name>.<env-default-domain>` on both the setup job and the service.

---

## 6. Rebuild reproducibility (`destroy` → `apply`)

`apply` alone does not bootstrap — it provisions; you re-run §2.

- **Postgres survives the destroy** (you only destroyed the ACA app/job units): the
  DB is already migrated and the master key unchanged, so the recreated service
  comes up healthy on apply with no manual steps (restart only if it raced).
- **Postgres is also destroyed** (fresh empty DB): repeat the full §2 bootstrap
  (grant → init → setup → restart). A fresh DB + proper init→setup order is clean —
  the stuck-migration case (§4) won't recur.

Cautions:
- The shared Postgres server also holds the `signalbeam` DB for the other
  microservices — **don't destroy it casually.**
- **Master key and DB must stay in sync.** Destroying Key Vault regenerates
  encryption keys; if the DB survives but the key changes, the app can't decrypt
  existing data. Keep both, or reset both together.

---

## 7. Make it turnkey (optional)

Terraform can't run manual-trigger jobs, but a post-apply `null_resource` with
`local-exec` (or a CI step) can call `az containerapp job start` for init/setup and
then restart the service — turning a fresh deploy into `apply` + one script. The
DB grant still needs the in-VNet job (§2a) since the runner can't reach private
Postgres directly.

---

**Related:** `infra/terragrunt/dev/aca/README.md` (live Zitadel runbook),
`docs/operations/deployment.md` (AKS vs ACA paths).
```
