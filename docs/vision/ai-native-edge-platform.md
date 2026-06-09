# Vision: SignalBeam as an AI‑Native Edge Platform

> **Status:** Draft / North Star · **Author:** makigjuro · **Date:** 2026-06-09
>
> This is a forward‑looking vision and target architecture. It extends — it does not
> replace — the current product defined in [`project-overview.md`](../project-overview.md)
> and [`architecture.md`](../architecture.md). Everything here is designed to be built
> in independently shippable slices on top of what already exists.

---

## 1. TL;DR

SignalBeam Edge already does the hard, unglamorous part: **onboard, observe, and update fleets
of edge devices** (`device → group → bundle → status`). The opportunity is to make the platform
**reason about** the fleet, not just move bytes to it.

The vision is to evolve SignalBeam from a *fleet management tool* into an
**AI‑native edge platform** with three stacked layers:

1. **Substrate** — what we have: deploy, manage, and observe devices and their containerized workloads.
2. **Intelligence** — predictive maintenance & anomaly detection on the telemetry we already collect.
3. **Brain** — a hybrid (edge + Claude) RAG/agent **copilot** that turns fleet telemetry, runbooks,
   and APIs into a conversation, and can *act* through the platform.

The differentiator: most fleet tools are dumb pipes. SignalBeam **predicts**, **explains**, and
(with guardrails) **acts** — privately at the edge where it matters, and with cloud‑grade reasoning
(Claude) where it counts.

---

## 2. The shift

| | Today (Substrate) | Target (AI‑native) |
|---|---|---|
| Visibility | Online/offline + metrics dashboards | "What's worrying about my fleet?" answered in natural language, grounded in real data |
| Failures | Seen after they happen | **Predicted** before they happen (anomaly + forecast) |
| Operations | Operator reads dashboards, pushes bundles by hand | Copilot proposes & (on approval) executes rollouts/restarts |
| AI workloads | N/A | First‑class: deploy local models/RAG to devices as managed bundles |
| Privacy | Telemetry to control plane | Edge‑local by default; cloud reasoning only on an explicit, redacted boundary |

The product tagline graduates from *"never SSH into 50 boxes"* to
**"your fleet, observed and reasoned about by an agent — predictive, private, and able to act."**

---

## 3. The three layers

### 3.1 🏗️ Substrate — *exists today*
The device‑side agent + backend services + web UI that already implement
`device → group → bundle → status`:

- **Edge Agent** (`src/EdgeAgent/`) — registration, heartbeats, container reconciliation, status. Ships as an ARM/x86 `.deb` + systemd unit.
- **DeviceManager** (`src/DeviceManager/`) — device identity, groups, tags, desired state.
- **BundleOrchestrator** (`src/BundleOrchestrator/`) — bundle assignment + rollout state.
- **TelemetryProcessor** (`src/TelemetryProcessor/`) — ingests metrics/telemetry into PostgreSQL/**TimescaleDB**.
- **IdentityManager** (`src/IdentityManager/`) — tenants, API keys, quotas.
- **API Gateway** (`src/SignalBeam.ApiGateway/`, YARP) + **Web UI** (`web/`).
- Messaging via **NATS/JetStream**; cache via **Valkey**; orchestration via **.NET Aspire** (`src/SignalBeam.AppHost`).

> In‑flight work that this vision builds on: the **EdgeAgent NATS push channel** epic (#281,
> issues #335–#338) gives us low‑latency control‑plane → device push, which the agentic
> "act" capability depends on. Also relevant: real device metrics (#280), self‑registration
> & key lifecycle (#279), smart reconciliation (#278), standalone resilience (#277).

### 3.2 🔮 Intelligence — *predictive maintenance & anomaly detection*
This is **not a new pipeline** — it's analytics on the time‑series TelemetryProcessor already
collects. Start statistical, evolve to ML.

- **Anomaly detection** — rolling z‑score / thresholds → Isolation Forest / LSTM (roadmap #249).
- **Failure prediction / forecasting** — ARIMA / Prophet style forecasting, error‑budget burn,
  "likely to fail within N days" (roadmap #250).
- Emits **Insight events** (anomaly, prediction) onto NATS and persists them for the UI and the Brain.
- Generalizes from device health (CPU/mem/disk/temp) to **IoT sensor signals** (vibration, current,
  temperature) ingested through the same telemetry path — the predictive‑maintenance use case.

**Proposed component:** `InsightEngine` — a new service (`src/InsightEngine/`) that reads the
TimescaleDB telemetry, runs detectors/forecasters, and publishes `insight.*` events. (Alternative:
start as a module inside TelemetryProcessor; promote to its own service when it grows. See Open Questions.)

### 3.3 🧠 Brain — *hybrid RAG + agent copilot*
The natural‑language + reasoning layer. It lives in **two places**:

**(a) Control‑plane copilot — `FleetCopilot` (`src/FleetCopilot/`)**
- A tool‑using agent (Claude) that answers operator questions grounded in fleet reality:
  *"Which devices will fail this week?"*, *"Why did the rollout to group X break?"*, *"Summarize fleet health."*
- **RAG** over runbooks/docs + recent telemetry summaries + Insight events.
- **Tools** = the existing SignalBeam APIs: list/inspect devices (DeviceManager), rollout state
  (BundleOrchestrator), insights (InsightEngine). Read‑only first; **actions** (propose/execute
  rollout, restart) added later behind approval + guardrails, executed via BundleOrchestrator and
  pushed over the NATS channel (#281).
- **Hybrid**: Claude for reasoning/synthesis; cheap/simple/structured steps can route to a small model.
- **Prompt caching** on the stable system prompt + tool schemas + slow‑changing context to cut cost/latency.

**(b) On‑device AI — the `edge-ai-node` bundle**
- A SignalBeam **bundle** (container image) running **Ollama** (small local model + local embeddings)
  + a lightweight RAG, deployed to devices *by SignalBeam itself*.
- Enables **offline/private** local analysis ("explain this anomaly locally"), local Q&A over
  device‑local docs, and resilience when disconnected.
- It is just a managed workload — SignalBeam deploys, updates, and monitors it like any other bundle.

---

## 4. Target architecture

```
┌─ OPERATOR (Web UI / chat / Tailscale) ───────────────────────────────────────┐
│  "How's my fleet? Anything about to fail? Fix group prod-eu."                 │
└───────────────────────────────┬───────────────────────────────────────────────┘
                                 │ via API Gateway (YARP)
                                 ▼
┌─ 🧠 BRAIN ── FleetCopilot (src/FleetCopilot) ────────────────────────────────┐
│  Claude (reasoning) + RAG (runbooks, telemetry summaries, insights)          │
│  Tool-use ──► DeviceManager · BundleOrchestrator · InsightEngine             │
│  Hybrid router: simple/structured → small model · complex → Claude           │
│  Privacy boundary + prompt caching                                           │
└───────────────┬───────────────────────────────────┬──────────────────────────┘
        reads   │                              acts  │ (approval + guardrails)
                ▼                                     ▼
┌─ 🔮 INTELLIGENCE ── InsightEngine ───────┐   ┌─ BundleOrchestrator ──────────┐
│  anomaly detection (#249)                │   │  rollout assignment + state   │
│  forecasting / failure prediction (#250) │   └───────────────┬───────────────┘
│  reads TimescaleDB · emits insight.* ────┼──► NATS/JetStream │ push (#281)
└───────────────▲──────────────────────────┘                   ▼
                │ telemetry              ┌─ 🏗️ SUBSTRATE ─────────────────────┐
┌─ TelemetryProcessor ──► PostgreSQL/TimescaleDB                              │
│  DeviceManager (identity/groups/state) · IdentityManager (tenants/keys)     │
└───────────────▲─────────────────────────────────────────────────────────────┘
                │ heartbeats / metrics / status            ▲ desired state / push
                │                                          │
┌─ EDGE DEVICES (Pi 5 = device #1) ────────────────────────┴───────────────────┐
│  EdgeAgent (.deb + systemd) ── reconciles containers via Docker              │
│  Bundles: sensor-reader · local-anomaly · edge-ai-node (Ollama + local RAG)  │
└───────────────────────────────────────────────────────────────────────────────┘
```

**Two loops:**
- **Up:** devices → telemetry → InsightEngine → insights → Brain/UI.
- **Down:** operator/Brain decision → BundleOrchestrator → NATS push (#281) → EdgeAgent → containers.

---

## 5. The virtuous loop (why this compounds)

**SignalBeam deploys the AI → the AI makes SignalBeam smarter → SignalBeam manages the AI's lifecycle.**

- The Intelligence layer feeds the Brain (insights become grounded context).
- The Brain can *act* through the Substrate (rollouts, restarts) with guardrails.
- The on‑device AI is delivered and updated *as a bundle* by the Substrate.
- Dogfooding: the maintainer's own **Raspberry Pi 5 is device #1** (reachable today over Tailscale),
  so the platform is validated end‑to‑end on real hardware from the first slice.

---

## 6. Privacy & trust model (the differentiator)

| Data | Where it lives | Leaves the device? |
|---|---|---|
| Raw sensor data, device‑local docs, embeddings | Edge device | **No** (edge‑local by default) |
| Aggregated metrics, anomalies, predictions | Control plane (tenant‑scoped) | Stays in SignalBeam |
| Operator query + retrieved/aggregated context | → Claude, only on the hybrid path | **Only across an explicit, redacted boundary** |

- **Local‑only mode**: content flagged sensitive is answered fully on‑device (Ollama), nothing to cloud.
- **Explicit boundary**: a redaction/allowlist step controls exactly what context reaches Claude; tenant‑configurable.
- **Auditable**: every cloud call and every agent *action* is logged (what was sent, what was done, by whom/which policy).
- Builds on existing security work: mTLS ([`architecture/mtls-architecture.md`](../architecture/mtls-architecture.md)),
  device auth ([`features/device-authentication.md`](../features/device-authentication.md)), API keys/quotas (IdentityManager).

---

## 7. Phased delivery plan

Each phase is independently valuable and shippable. Phases 0–2 form the **thin vertical slice**
that proves the whole concept with a single device.

### Phase 0 — Substrate / dogfood *(foundation)*
- EdgeAgent installed on the Pi 5; real metrics flowing into TelemetryProcessor.
- Depends on / advances: real metrics (#280), self‑registration (#279).
- **Done when:** the Pi appears in the dashboard with live, real heartbeats + metrics.

### Phase 1 — Intelligence MVP
- `InsightEngine` v0: rolling z‑score / threshold anomaly detection on device metrics → `insight.anomaly` events → surfaced in UI + NATS.
- Seeds #249.
- **Done when:** an induced anomaly on the Pi (e.g. CPU spike, disk fill) is detected and shown.

### Phase 2 — Brain MVP (read‑only copilot)
- `FleetCopilot` v0: Claude tool‑using agent answering fleet‑health questions, grounded via tools
  (list devices, get health, get anomalies) + light RAG over runbooks. CLI/endpoint first, then Web UI panel.
- Prompt caching on system prompt + tool schemas.
- **Done when:** *"How's my fleet, anything worrying?"* returns an accurate, cited answer from real data.

### Phase 3 — Predictive
- Forecasting / failure prediction in InsightEngine (#250); copilot answers *"what's likely to fail and when."*

### Phase 4 — On‑device AI
- Package the `edge-ai-node` bundle (Ollama + local RAG); deploy to the Pi via SignalBeam.
- **Done when:** the Pi answers a local query / explains an anomaly **offline**, with the bundle managed by SignalBeam.

### Phase 5 — Agentic actions
- Copilot can propose and (on approval) execute rollouts/restarts via BundleOrchestrator, delivered
  over the NATS push channel (#281), behind policy + audit.

### Phase 6 — Scale & polish
- Multi‑sensor IoT ingestion, richer dashboards, alert correlation, SLOs — aligns with roadmap phases (#248–#256).

---

## 8. Non‑goals (scope guardrails)

- **Not** replacing the core `device → group → bundle → status` model — augmenting it.
- **Not** a heavyweight cloud‑IoT platform; stay opinionated and minimal.
- **Not** large‑model inference on the Pi — heavy reasoning stays in Claude; on‑device models are small and purposeful.
- **Not** autonomous, unguarded actions — agent actions require approval + audit until trust is earned.

---

## 9. Risks & considerations

| Risk | Mitigation |
|---|---|
| Pi hardware limits (CPU‑only inference, SD‑card I/O) | Keep heavy reasoning in Claude; local models ≤3B; recommend NVMe for serious nodes |
| Cloud (Claude) cost | Prompt caching; route simple/structured steps to local models; batch; cache insight summaries |
| Privacy boundary correctness | Explicit allowlist/redaction; local‑only mode; full audit of cloud calls |
| Security of agent actions | Least‑privilege agent identity; approval gates; mTLS; per‑tenant scoping |
| Scope creep | Strict phasing; every phase independently shippable |
| Model/insight quality | Start statistical & explainable before ML; always cite grounding data |

---

## 10. Open questions

- **InsightEngine**: standalone service (`src/InsightEngine/`) from day one, or a module inside
  TelemetryProcessor that we promote later?
- **FleetCopilot hosting**: new service vs. capability behind the API Gateway; where the agent loop
  and vector store live (control‑plane Qdrant vs. per‑device).
- **Model selection**: which local models (Ollama `llama3.2:3b` / `qwen2.5:3b`, `nomic-embed-text`),
  and which Claude tier per task (Haiku for cheap/structured, Sonnet for reasoning).
- **Action surface & guardrails**: which operations the copilot may take, and the approval UX.
- **Tenancy**: how copilot context and insights are isolated per tenant.

---

## 11. References

- [`docs/project-overview.md`](../project-overview.md) — current product & services
- [`docs/architecture.md`](../architecture.md) — current system architecture
- [`docs/architecture/technical-architecture.md`](../architecture/technical-architecture.md)
- [`docs/architecture/mtls-architecture.md`](../architecture/mtls-architecture.md) · [`docs/features/device-authentication.md`](../features/device-authentication.md)
- [`docs/features/rollouts.md`](../features/rollouts.md)
- Roadmap issues: NATS push channel **#281** (#335–#338) · real metrics **#280** · self‑registration **#279** · smart reconciliation **#278** · standalone resilience **#277** · ML anomaly detection **#249** · predictive analytics **#250**
