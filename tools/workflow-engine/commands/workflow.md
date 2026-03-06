---
name: workflow
description: "Workflow state machine operations: transition, run-gate, signal-done, status"
---

Run workflow operations. Only the lead agent should call transitions.

**Usage:** `/workflow-engine:workflow <command> [args]`

**Commands:**
- `transition <STATE>` — Transition to a new state (enforces guards)
- `run-gate <STATE>` — Run a deterministic gate (build, lint, test)
- `signal-done` — Developer signals implementation complete
- `review-done <APPROVED|REJECTED>` — Reviewer signals review complete
- `verify-done <PASS|FAIL>` — Reviewer signals QA verification complete
- `status` — Show current workflow state
- `record-issue <NUMBER>` — Record the GitHub issue number
- `record-branch <NAME>` — Record the feature branch name
- `record-pr <NUMBER>` — Record the PR number

Run:

```bash
CLAUDE_PLUGIN_ROOT="${CLAUDE_PLUGIN_ROOT}" npx tsx "${CLAUDE_PLUGIN_ROOT}/src/workflow.ts" $ARGUMENTS
```
