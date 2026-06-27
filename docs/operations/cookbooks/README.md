# Operations Cookbooks

Reusable, parameterized recipes for recurring operational tasks. Each is written
to adapt across environments (`dev` / staging / prod) and, where noted, across
services — edit the variables block at the top and follow along.

| Cookbook | Use when |
|----------|----------|
| [Stateful app with DB migrations on ACA](stateful-app-with-migrations-on-aca.md) | Deploying a service that needs one-shot DB init/migration before it can serve (Zitadel, Keycloak, etc.) on Azure Container Apps — using the init→setup→start job split to avoid migration-race deadlocks |
| [Onboard an EdgeAgent against the cloud](onboard-edge-agent-against-cloud.md) | Registering a Raspberry Pi / EdgeAgent against a deployed control plane (ACA) end-to-end — register → heartbeat → reconcile, plus scale-to-zero cold-start gotchas |

For full deployment paths (AKS vs ACA) see [`../deployment.md`](../deployment.md).
