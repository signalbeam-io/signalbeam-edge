---
name: start-feature
description: "Start a feature development workflow from a GitHub issue"
---

Initialize the workflow state machine and spawn the agent team to implement a feature.

**Usage:** `/workflow-engine:start-feature <issue-number>`

Run:

```bash
CLAUDE_PLUGIN_ROOT="${CLAUDE_PLUGIN_ROOT}" npx tsx "${CLAUDE_PLUGIN_ROOT}/src/workflow.ts" init $ARGUMENTS
```
