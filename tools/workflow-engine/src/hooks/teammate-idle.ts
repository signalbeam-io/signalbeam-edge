import type { HookInput, OperationResult } from "../types.js";
import { loadWorkflowState } from "../persistence/state-store.js";
import { getStateEmoji } from "../engine/state-machine.js";
import { allow, block } from "./hook-io.js";

export function handleTeammateIdle(input: HookInput, _pluginRoot: string): OperationResult {
  const state = loadWorkflowState(input.session_id);
  if (!state) return allow();

  // Lead can only be idle in BLOCKED or COMPLETE
  if (input.agent_name === "workflow-lead") {
    if (state.state !== "BLOCKED" && state.state !== "COMPLETE") {
      return block(
        `${getStateEmoji(state)} Lead cannot go idle in ${state.state}. ` +
        "Follow the state procedure checklist or transition to BLOCKED if stuck.",
      );
    }
  }

  // Developer cannot go idle in FIXING without signalling done
  if (input.agent_name?.startsWith("workflow-developer") || input.agent_name?.startsWith("fixer")) {
    if (state.state === "FIXING" && !state.developerDone) {
      return block(
        `${getStateEmoji(state)} Developer cannot go idle during FIXING without signalling done. ` +
        "Run /workflow-engine:workflow signal-done when implementation is complete.",
      );
    }
  }

  return allow();
}
