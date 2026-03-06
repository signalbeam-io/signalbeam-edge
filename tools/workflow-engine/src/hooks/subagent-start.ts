import type { HookInput, OperationResult } from "../types.js";
import { loadWorkflowState, saveWorkflowState } from "../persistence/state-store.js";
import { allow, injectContext } from "./hook-io.js";

export function handleSubagentStart(input: HookInput, pluginRoot: string): OperationResult {
  const state = loadWorkflowState(input.session_id);
  if (!state) return allow();

  // Register agent in active agents list
  if (input.agent_name && !state.activeAgents.includes(input.agent_name)) {
    const updated = {
      ...state,
      activeAgents: [...state.activeAgents, input.agent_name],
    };
    saveWorkflowState(input.session_id, updated);
  }

  // Inject iteration context into spawned agents
  const context = [
    `Workflow state: ${state.state}`,
    `Issue: #${state.issueNumber ?? "N/A"}`,
    `Branch: ${state.branch || "N/A"}`,
    `Fix attempt: ${state.fixAttempts}/3`,
  ];

  if (state.reviewReport) {
    context.push(`\nPrevious review findings:\n${state.reviewReport.slice(0, 2000)}`);
  }
  if (state.verifyReport) {
    context.push(`\nPrevious QA findings:\n${state.verifyReport.slice(0, 2000)}`);
  }

  return injectContext(context.join("\n"));
}
