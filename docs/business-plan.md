# SignalBeam Edge – Business & MVP Plan

## 1. One-sentence pitch

SignalBeam Edge lets you **onboard, monitor, and update fleets of edge devices (e.g., Raspberry Pis running containers)** from a single dashboard, so you never have to SSH into 50 boxes again.

---

## 2. Market research & pain points

### 2.1 Existing solution categories

Today, teams typically use one or more of the following for edge/device fleets:

1. **Hyperscaler IoT stacks**
   - Azure IoT Hub + IoT Edge
   - AWS IoT Core + Greengrass

2. **Container/Kubernetes-based edge tools**
   - Rancher Fleet + k3s / other edge K8s
   - Azure Arc + GitOps (Flux/Argo) for edge clusters

3. **IoT device/fleet platforms**
   - balenaOS / balenaCloud / openBalena
   - OTA-focused tools such as Mender or SWUpdate

Most of these tools were designed for large enterprises or full IoT programs, not for small/medium fleets owned by a handful of engineers.

---

### 2.2 Key pain points in current solutions

#### 1) High complexity & steep learning curve

- Azure IoT Edge / AWS Greengrass require understanding many moving parts (IoT Hub/Core, routes, modules, policies, certificates, device twins, etc.).
- Getting from “Raspberry Pi on my desk” to “10 devices in the field with reliable updates” is non-trivial.
- Small teams feel they must become cloud and PKI experts to simply deploy containers to edge devices.

**Implication for SignalBeam:** Focus on a radically simpler mental model:
> *device → group → bundle → status*  
No DPS, twins, routes or multi-service configuration pages for v1.

---

#### 2) Operational unreliability & rough developer experience

- Developers complain about flaky dev loops and opaque failures, especially with Greengrass and complex IoT setups.
- When something goes wrong (cert, policy, routing, or connectivity), debugging is painful and often requires SSH access and deep cloud expertise.

**Implication for SignalBeam:** Emphasise:
- a robust, self-diagnosing **agent**, and
- very clear error surfaces in the UI (“image pull failed: registry unreachable”, “auth token invalid”, etc.).

---

#### 3) Overkill for small/medium fleets

- Many platforms assume 100s–1000s of devices and a dedicated platform team.
- But a large portion of engineers just have **5–200 devices** and want:
  - “Push this version to all my Pis”
  - “Show me what’s running where”
  - “Tell me which ones are broken and why”

**Implication for SignalBeam:** Design the product explicitly for:
- small and mid-sized fleets (from home labs to the first factory/plant),
- with minimal concepts and a fast path to value.

---

#### 4) Pricing and cost-structure pain

- Platforms like balenaCloud offer a good DX but become expensive at scale with pure per-device pricing.
- Projects with many low-margin devices or tight budgets struggle to justify ongoing per-device fees.

**Implication for SignalBeam:** 
- Prefer **tiered pricing per tenant** (with generous device limits) over strict per-device pricing.
- Make it economical to run dozens or hundreds of cheap edge nodes without bill shock.

---

#### 5) OS & firmware updates are still messy

- Many tools handle **container/app updates** well but treat OS or firmware updates as second-class or leave them to other tools.
- Safe OTA OS updates (A/B partitions, health checks, rollback) are complex and easy to get wrong.
- Users report frustration and even switch platforms purely over OS update pain.

**Implication for SignalBeam (roadmap):**
- MVP: focus on containerized applications.
- vNext: make safe OS/firmware updates a first-class workflow with opinionated best practices.

---

#### 6) Poor fleet observability & feedback

- GitOps-based tools like Rancher Fleet are great at pushing desired state but can lack rich feedback on **why** a device/cluster is not in sync.
- IoT dashboards often show only “connected/disconnected” instead of “exact app version and health per device”.

**Implication for SignalBeam:**
- Put **fleet visibility** at the core:
  - “What’s running where?”
  - “Which devices failed an update and for what reason?”
- Provide per-bundle rollout views with detailed, actionable failure reasons.

---

#### 7) Networking, security, and PKI overhead

- Current platforms have complex security and networking stories (cert management, policies, VPNs, role-based access, etc.).
- While powerful, they are often overkill and intimidating for teams with a few edge nodes and limited security experience.

**Implication for SignalBeam:**
- Hide complexity behind sane, secure defaults.
- Start with a simple but solid model (per-tenant key + per-device identity) and evolve to full mTLS and PKI in later releases.
- Offer clear, practical documentation for common network topologies (NAT, firewalled sites, DMZ).

---

#### 8) Fragmented tooling

- Real-world fleets often combine:
  - One system for identity (IoT Hub/Core),
  - Another for app deployment (IoT Edge, Greengrass, Fleet),
  - Another for OTA OS updates (Mender, SWUpdate),
  - Another for logs/metrics,
  - Another for AI/ML model distribution.
- Each additional system adds IAM, credentials, dashboards, and cognitive load.

**Implication for SignalBeam:**
- Be an opinionated **“90% stack”**:
  - Device registration,
  - Container deployment,
  - Basic metrics & health,
  - Rollout status and alerts.
- Integrate cleanly with external logging/monitoring tools rather than forcing multiple dashboards.

---

#### 9) Edge AI: closed and heavy for small teams

- Enterprise edge AI platforms assume big budgets, lots of devices, and deep cloud integration.
- Small teams with a few vision or anomaly-detection models find these stacks too heavy, too closed, or too cloud-specific.

**Implication for SignalBeam (roadmap):**
- Treat AI models as **just another bundle artifact** (e.g., ONNX/TensorRT package).
- Start with simple support:
  - ship models to devices,
  - expose basic inference metrics and health.
- Avoid tying users to a single cloud or MLOps platform.

---

### 2.3 Summary of market gap

There is space for a product that is:

- Simpler than hyperscaler IoT stacks,
- More opinionated and integrated than pure GitOps tools,
- More affordable and transparent than some per-device IoT fleet platforms,
- Designed around **5–200 devices**, not just huge fleets,
- Future-proofed for OS updates and AI without forcing them into v1.

**SignalBeam Edge** aims to occupy this middle ground.

---

## 3. Target user & initial use case

### 3.1 Primary user persona

- **Role:** Senior engineer / architect responsible for edge nodes (industrial IT, OT, smart building, lab equipment, home lab dev).
- **Pain:**
  - Manually managing **N edge devices** (Raspberry Pi / mini-PC).
  - SSH’ing into each device to:
    - check if services are running,
    - update Docker images / k3s deployments,
    - gather logs when something breaks.

### 3.2 Initial use case (MVP slice)

> “I have 5–20 edge devices (Pis or small x86 boxes) that run dockerized apps.  
> I want to see if they’re **online**, which **version** they’re running, and roll out **new versions** to all of them (or a group) with minimal friction.”

---

## 4. Value proposition & differentiation

Based on the pain points:

1. **Radical simplicity**
   - Conceptual model: *device → group → bundle → status*.
   - Minimal setup: install agent, register device, assign bundle.

2. **First-class fleet visibility**
   - Always know what’s running where.
   - Clear rollout status and error reasons.

3. **Designed for small/medium fleets**
   - From 5–200 devices, with pricing and UX aligned to small teams.
   - No need for a full IoT department.

4. **Opinionated but open**
   - Covers 80–90% of common needs (identity, deployment, health, status) in one product.
   - Clean integration with external logging/monitoring and any cloud.

5. **Roadmap to OS & AI**
   - Start with app/container updates.
   - Add safe OS updates and “model as bundle” as customers grow.

---

## 5. MVP scope (what v0.1 will do)

### 5.1 Core capabilities

1. **Device registration & identity**
   - Each edge device runs a **SignalBeam Edge Agent**.
   - Agent registers with the cloud using:
     - `tenantId`
     - `deviceId`
     - registration token (simple shared secret for MVP).
   - After registering, the device appears in the **Fleet Dashboard**.

2. **Heartbeat & basic health**
   - Agent periodically sends:
     - `online` status (heartbeat)
     - uptime
     - CPU usage (avg)
     - memory usage (avg)
     - disk usage (%)
   - Dashboard shows:
     - Online / offline indicator.
     - Last seen timestamp.
     - Basic metrics snapshot.

3. **Device grouping & tags**
   - Ability to:
     - Tag devices: `lab`, `prod`, `line-1`, `rpi`, `x86`.
     - Create logical groups: `Group A – Testing`, `Group B – Production`.
   - Groups used as **rollout targets**.

4. **Application bundle definition**
   - In cloud UI/API, define **App Bundles**:
     - A named bundle with:
       - `bundleId`, `version`
       - list of containers + images (e.g., JSON or docker-compose-like spec).
   - Example bundle:
     ```json
     {
       "bundleId": "warehouse-monitor",
       "version": "1.2.0",
       "containers": [
         { "name": "temp-sensor", "image": "ghcr.io/org/temp-sensor:1.2.0" },
         { "name": "relay-controller", "image": "ghcr.io/org/relay-controller:2.0.1" }
       ]
     }
     ```

5. **Assigning bundles to devices/groups**
   - From the dashboard:
     - assign `bundleId + version` to:
       - a single device, or
       - a group of devices.
   - This creates a **desired state** per device.

6. **Agent reconciliation loop**
   - Agent periodically:
     - pulls its **desired bundle** definition.
     - compares with **currently running containers**.
     - pulls missing images and starts them.
     - optionally stops containers that are no longer in the bundle.
   - Agent reports:
     - current bundle version
     - container statuses (Running / Exited).

7. **Simple rollout status**
   - For a given bundle version:
     - see rollout status across devices:
       - Pending (not yet updated)
       - Updating
       - Succeeded
       - Failed (with simple reason).

8. **Basic event log / activity history**
   - Per device:
     - registration events
     - config changes (bundle assignments)
     - state transitions (online/offline, bundle updates).
   - MVP: simple chronological list.

---

## 6. Architecture overview (MVP)

### 6.1 Components

1. **SignalBeam Edge Agent (on device)**
   - Language: `.NET` (console app / service).
   - Responsibilities:
     - Register device with cloud.
     - Send heartbeat + basic metrics at interval.
     - Fetch desired bundle for this device.
     - Manage containers (Docker CLI / Docker SDK / containerd).
     - Report bundle and container status.

2. **SignalBeam Cloud API**
   - Language: `.NET` (Web API / minimal API).
   - Responsibilities:
     - Device registration & auth.
     - Store devices, groups, tags.
     - CRUD for App Bundles.
     - Store desired state (bundle assignment).
     - Receive heartbeats & metrics from agents.
     - Provide rollout state and device status for the UI.

3. **SignalBeam Web UI**
   - Tech: React + TypeScript (simple SPA).
   - Responsibilities:
     - Fleet overview (table + detail view).
     - Device detail page (health, metrics snapshot, activity log).
     - Manage App Bundles.
     - Assign bundles to devices/groups.
     - See rollout progress.

4. **Data store**
   - MVP: PostgreSQL (or any relational DB).
   - Entities:
     - `Device`
     - `DeviceTag`
     - `DeviceGroup`
     - `AppBundle`
     - `AppBundleVersion`
     - `DeviceDesiredState`
     - `DeviceReportedState`
     - `DeviceEvent` / `ActivityLog`

### 6.2 Basic flows

#### Flow: Register device

1. User installs agent on device and runs:
   ```bash
   signalbeam-agent register --tenant-id <T> --device-id <D> --token <REG_TOKEN>
   ```
2. Agent calls `POST /api/devices/register`.
3. Cloud:
   - validates token.
   - creates/updates device record.
4. Device appears in UI as “Online / Newly registered”.

#### Flow: Assign bundle

1. User creates bundle `warehouse-monitor:1.2.0`.
2. In UI:
   - select group `Warehouse-Pis`.
   - assign `warehouse-monitor:1.2.0`.
3. Cloud:
   - sets desired state for all devices in that group.
4. Agents:
   - on next poll, see new desired bundle, pull images, start containers.
5. UI:
   - shows rollout: `0/5 updated → 3/5 → 5/5`.

---

## 7. User experience (MVP screens)

### 7.1 Fleet Overview

- **Table of devices**:
  - Device ID
  - Online/Offline indicator
  - Last seen
  - Current bundle & version
  - CPU / RAM snapshot
  - Tags
- Actions:
  - Filter by tags / groups.
  - Click row → Device Detail.

### 7.2 Device Detail

- Header: Device ID, status, last seen.
- Sections:
  - **Overview:**
    - Tags
    - Group membership
    - Current bundle/version
  - **Health:**
    - CPU, RAM, disk (%)
    - Simple “healthy/unhealthy” state (MVP heuristic).
  - **Containers:**
    - List of containers (name, image, status).
  - **Activity log:**
    - Recent events (registration, bundle changed, update success/failure).

### 7.3 Bundles

- **Bundle List**:
  - Name, latest version, number of devices running it.
- **Bundle Detail**:
  - Versions list.
  - For a selected version:
    - JSON/visual representation of containers.
    - “Assign to group…” action.

### 7.4 Simple Rollout View

- For a given bundle version:
  - show target group(s) & how many devices:
    - Pending / Updating / Succeeded / Failed.

---

## 8. Technical stack (MVP suggestion)

- **Agent**
  - Language: .NET
  - Platform: Linux (ARM/x86) first, systemd service.
  - Container management: Docker CLI or SDK.

- **Cloud Backend**
  - .NET 8 Web API (REST; GraphQL later if desired).
  - Auth: simple API key / token per tenant for MVP.
  - DB: PostgreSQL.
  - Hosting: any (Azure Container Apps, AKS, etc.).

- **Frontend**
  - React + TypeScript.
  - Component library: minimal (e.g., Tailwind or basic component lib).

---

## 9. MVP success metrics

For v0.1, success is qualitative and usage-based, not revenue-based.

- You can:
  - Onboard at least **3–5 devices**.
  - See **real-time online/offline** states.
  - Roll out bundle updates successfully to all of them.
- Manual SSH time:
  - “I updated all 5 devices without SSH’ing into any of them.”

Later metrics (post-MVP):

- D7/D30 retention for early users.
- Number of devices per tenant.
- Number of bundle deployments per month.

---

## 10. Roadmap after MVP (vNext ideas)

1. **Security**
   - Per-device certificates, mTLS.
   - RBAC for users in the UI.

2. **Smart Signals**
   - Rules engine at the edge for sensor data.
   - Alerts pipeline to the cloud.

3. **AI on the Edge**
   - Model distribution & versioning.
   - Monitoring inference stats and model “health signals”.

4. **OS/Firmware Updates**
   - A/B partitions where possible.
   - Health checks and automatic rollback.

5. **More advanced rollouts**
   - Canary updates.
   - Rollback on failure thresholds.

---

## 11. Open design questions (to validate with early users)

- Do they typically run **Docker**, **k3s**, or something else on edge devices?
- Do they prefer to:
  - manage containers directly, or
  - manage “apps” abstracted from containers?
- How often do they update apps?
- What is more painful: **“not knowing what’s running where”** or **“updating it everywhere”**?
- How critical are OS updates and AI in their first 12–18 months?

These answers will refine what SignalBeam Edge focuses on after the initial MVP.
