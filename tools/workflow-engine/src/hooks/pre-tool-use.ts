import type { HookInput, OperationResult, WorkflowState } from "../types.js";
import { loadWorkflowState } from "../persistence/state-store.js";
import { getStateEmoji, getStateSkill, getStageType } from "../engine/state-machine.js";
import { loadSkill } from "../skills/loader.js";
import { buildSystemPrompt } from "../skills/mapper.js";
import { allow, block, injectContext } from "./hook-io.js";

export function handlePreToolUse(input: HookInput, pluginRoot: string): OperationResult {
  const state = loadWorkflowState(input.session_id);
  if (!state) return allow();

  const emoji = getStateEmoji(state);
  const stageType = getStageType(state);

  // Block file writes during agent-review stages
  if (stageType === "agent" && (state.state === "REVIEWING" || state.state === "VERIFYING")) {
    if (input.tool_name === "Edit" || input.tool_name === "Write") {
      return block(`${emoji} File writes are blocked during ${state.state}.`);
    }
    if (input.tool_name === "Bash") {
      const cmd = String(input.tool_input?.command ?? "");
      if (cmd.match(/git\s+(commit|push|reset|checkout|merge|rebase)/)) {
        return block(`${emoji} Git write operations are blocked during ${state.state}.`);
      }
    }
  }

  // Block commits in FIXING before signal-done
  if (state.state === "FIXING" && input.tool_name === "Bash") {
    const cmd = String(input.tool_input?.command ?? "");
    if (cmd.match(/git\s+commit/) && !state.developerDone) {
      return block(`${emoji} Cannot commit during FIXING before signalling done.`);
    }
  }

  // Build context injection: status bar + skill content
  const repoRoot = process.env.REPO_ROOT ?? process.cwd();
  const statusBar = formatStatusBar(state);
  const skillContent = loadSkillForState(state, repoRoot);

  if (skillContent) {
    return injectContext(`${statusBar}\n\n---\n\n${skillContent}`);
  }

  return injectContext(statusBar);
}

function formatStatusBar(state: WorkflowState): string {
  const emoji = getStateEmoji(state);
  const gates = Object.entries(state.gateResults);
  const passedGates = gates.filter(([, r]) => r.passed).length;

  const parts = [
    `${emoji} ${state.state}`,
    `Issue #${state.issueNumber ?? "N/A"}`,
    `Branch: ${state.branch || "N/A"}`,
  ];

  if (gates.length > 0) {
    parts.push(`Gates: ${passedGates}/${gates.length} passed`);
  }
  if (state.fixAttempts > 0) {
    parts.push(`Fix attempt: ${state.fixAttempts}/3`);
  }
  if (state.reviewApproved !== null) {
    parts.push(`Review: ${state.reviewApproved ? "APPROVED" : "REJECTED"}`);
  }
  if (state.verifyPassed !== null) {
    parts.push(`QA: ${state.verifyPassed ? "PASS" : "FAIL"}`);
  }

  return parts.join(" | ");
}

function loadSkillForState(state: WorkflowState, repoRoot: string): string | null {
  const skillName = getStateSkill(state);
  if (!skillName) return null;

  const skill = loadSkill(repoRoot, skillName);
  if (!skill) return null;

  return buildSystemPrompt(skill);
}
