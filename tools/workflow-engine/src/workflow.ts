#!/usr/bin/env node

import { readFileSync } from "node:fs";
import type { HookInput, OperationResult, WorkflowState } from "./types.js";
import { EXIT_ALLOW, EXIT_ERROR } from "./types.js";
import {
  createInitialState,
  loadWorkflowState,
  saveWorkflowState,
  sessionExists,
  appendEvent,
} from "./persistence/state-store.js";
import { transition, isOperationAllowed, getStateSkill, getStageType } from "./engine/state-machine.js";
import { buildGitInfo } from "./engine/context.js";
import { runGate } from "./engine/executor.js";
import { loadSkill } from "./skills/loader.js";
import { buildSystemPrompt } from "./skills/mapper.js";
import { loadConfig } from "./config.js";
import { handleSessionStart } from "./hooks/session-start.js";
import { handlePreToolUse } from "./hooks/pre-tool-use.js";
import { handleSubagentStart } from "./hooks/subagent-start.js";
import { handleTeammateIdle } from "./hooks/teammate-idle.js";
import { allow } from "./hooks/hook-io.js";

function getPluginRoot(): string {
  return process.env.CLAUDE_PLUGIN_ROOT ?? process.cwd();
}

function getRepoRoot(): string {
  return process.env.REPO_ROOT ?? process.cwd();
}

function getSessionId(): string {
  return process.env.CLAUDE_SESSION_ID ?? "default";
}

// ── Hook dispatch ──

const HOOK_HANDLERS: Record<string, (input: HookInput, pluginRoot: string) => OperationResult> = {
  SessionStart: handleSessionStart,
  PreToolUse: handlePreToolUse,
  SubagentStart: handleSubagentStart,
  TeammateIdle: handleTeammateIdle,
};

function runHookMode(): OperationResult {
  let raw: string;
  try {
    raw = readFileSync(0, "utf-8");
  } catch {
    return allow();
  }

  let input: HookInput;
  try {
    input = JSON.parse(raw) as HookInput;
  } catch {
    return allow();
  }

  const handler = HOOK_HANDLERS[input.hook_event_name];
  if (!handler) return allow();

  return handler(input, getPluginRoot());
}

// ── Command handlers ──

type CommandHandler = (args: string[]) => OperationResult;

function requireState(): WorkflowState {
  const state = loadWorkflowState(getSessionId());
  if (!state) throw new Error("No active workflow. Run /workflow-engine:start-feature first.");
  return state;
}

function save(state: WorkflowState): void {
  saveWorkflowState(getSessionId(), state);
}

function loadSkillHint(stateName: string): string {
  // Create a temporary state to look up the skill
  const tempState = { ...createInitialState(), state: stateName as import("./types.js").StateName };
  const skillName = getStateSkill(tempState);
  const stageType = getStageType(tempState);

  if (!skillName) {
    return stageType === "gate"
      ? `[${stageType}] Run: /workflow-engine:workflow run-gate ${stateName}`
      : `[${stageType}] No skill mapped for ${stateName}`;
  }

  const repoRoot = getRepoRoot();
  const skill = loadSkill(repoRoot, skillName);
  if (!skill) return `Skill '${skillName}' not found in .claude/skills/`;

  const prompt = buildSystemPrompt(skill);

  return `[${stageType}] Skill: ${skillName}\nTools: ${skill.allowedTools.join(", ")}\n\n${prompt}`;
}

function handleInit(args: string[]): OperationResult {
  const issueNumber = parseInt(args[1], 10);
  if (isNaN(issueNumber)) {
    return { output: "Usage: init <issue-number>", exitCode: EXIT_ERROR };
  }

  const sid = getSessionId();
  if (sessionExists(sid)) {
    return { output: "Workflow already initialized for this session.", exitCode: EXIT_ALLOW };
  }

  const state = createInitialState();
  state.issueNumber = issueNumber;
  save(appendEvent(state, "init", { issueNumber }));

  const skillHint = loadSkillHint("PREFLIGHT");

  return {
    output: `Workflow initialized. Issue: #${issueNumber}\nState: PREFLIGHT\n\n${skillHint}`,
    exitCode: EXIT_ALLOW,
  };
}

function handleTransition(args: string[]): OperationResult {
  const target = args[1]?.toUpperCase();
  if (!target) {
    return { output: "Usage: transition <STATE>", exitCode: EXIT_ERROR };
  }

  const state = requireState();
  const config = loadConfig(getRepoRoot());
  const gitInfo = buildGitInfo(getRepoRoot());

  const { result, newState } = transition(state, target, config, gitInfo);

  if (!result.pass) {
    save(appendEvent(state, "transition-blocked", { target, reason: result.reason }));
    return { output: `Transition BLOCKED: ${result.reason}`, exitCode: EXIT_ERROR };
  }

  save(newState);

  const skillHint = loadSkillHint(target);

  return {
    output: `Transitioned to ${target}\n\n${skillHint}`,
    exitCode: EXIT_ALLOW,
  };
}

function handleRunGate(args: string[]): OperationResult {
  const gateName = args[1]?.toUpperCase();
  if (!gateName) {
    return { output: "Usage: run-gate <GATE>", exitCode: EXIT_ERROR };
  }

  const state = requireState();
  const opCheck = isOperationAllowed(state, "run-gate");
  if (!opCheck.pass) {
    return { output: `Operation blocked: ${opCheck.reason}`, exitCode: EXIT_ERROR };
  }

  const config = loadConfig(getRepoRoot());
  const result = runGate(gateName, state, config, getRepoRoot());

  const updated = {
    ...state,
    gateResults: {
      ...state.gateResults,
      [gateName]: { passed: result.status === "passed", output: result.output, duration_ms: result.duration_ms },
    },
  };
  save(appendEvent(updated, "gate-result", { gate: gateName, passed: result.status === "passed" }));

  const status = result.status === "passed" ? "PASS" : "FAIL";
  return {
    output: `Gate ${gateName}: ${status} (${(result.duration_ms / 1000).toFixed(1)}s)\n${result.output}`,
    exitCode: result.status === "passed" ? EXIT_ALLOW : EXIT_ERROR,
  };
}

function handleRecordIssue(args: string[]): OperationResult {
  const num = parseInt(args[1], 10);
  if (isNaN(num)) return { output: "Usage: record-issue <NUMBER>", exitCode: EXIT_ERROR };
  const state = requireState();
  save(appendEvent({ ...state, issueNumber: num }, "record-issue", { issueNumber: num }));
  return { output: `Issue recorded: #${num}`, exitCode: EXIT_ALLOW };
}

function handleRecordBranch(args: string[]): OperationResult {
  const branch = args[1];
  if (!branch) return { output: "Usage: record-branch <NAME>", exitCode: EXIT_ERROR };
  const state = requireState();
  save(appendEvent({ ...state, branch }, "record-branch", { branch }));
  return { output: `Branch recorded: ${branch}`, exitCode: EXIT_ALLOW };
}

function handleRecordPr(args: string[]): OperationResult {
  const num = parseInt(args[1], 10);
  if (isNaN(num)) return { output: "Usage: record-pr <NUMBER>", exitCode: EXIT_ERROR };
  const state = requireState();
  save(appendEvent({ ...state, prNumber: num }, "record-pr", { prNumber: num }));
  return { output: `PR recorded: #${num}`, exitCode: EXIT_ALLOW };
}

function handleSignalDone(_args: string[]): OperationResult {
  const state = requireState();
  const opCheck = isOperationAllowed(state, "signal-done");
  if (!opCheck.pass) return { output: `Operation blocked: ${opCheck.reason}`, exitCode: EXIT_ERROR };

  save(appendEvent({ ...state, developerDone: true }, "signal-done", {}));
  return { output: "Developer signalled done.", exitCode: EXIT_ALLOW };
}

function handleReviewDone(args: string[]): OperationResult {
  const verdict = args[1]?.toUpperCase();
  if (verdict !== "APPROVED" && verdict !== "REJECTED") {
    return { output: "Usage: review-done <APPROVED|REJECTED>", exitCode: EXIT_ERROR };
  }
  const state = requireState();
  const opCheck = isOperationAllowed(state, "review-done");
  if (!opCheck.pass) return { output: `Operation blocked: ${opCheck.reason}`, exitCode: EXIT_ERROR };

  const report = args.slice(2).join(" ");
  save(appendEvent(
    { ...state, reviewApproved: verdict === "APPROVED", reviewReport: report },
    "review-done",
    { approved: verdict === "APPROVED" },
  ));
  return { output: `Review: ${verdict}`, exitCode: EXIT_ALLOW };
}

function handleVerifyDone(args: string[]): OperationResult {
  const verdict = args[1]?.toUpperCase();
  if (verdict !== "PASS" && verdict !== "FAIL") {
    return { output: "Usage: verify-done <PASS|FAIL>", exitCode: EXIT_ERROR };
  }
  const state = requireState();
  const opCheck = isOperationAllowed(state, "verify-done");
  if (!opCheck.pass) return { output: `Operation blocked: ${opCheck.reason}`, exitCode: EXIT_ERROR };

  const report = args.slice(2).join(" ");
  save(appendEvent(
    { ...state, verifyPassed: verdict === "PASS", verifyReport: report },
    "verify-done",
    { passed: verdict === "PASS" },
  ));
  return { output: `QA Verification: ${verdict}`, exitCode: EXIT_ALLOW };
}

function handleStatus(_args: string[]): OperationResult {
  const state = loadWorkflowState(getSessionId());
  if (!state) return { output: "No active workflow.", exitCode: EXIT_ALLOW };

  const lines = [
    `State:       ${state.state}`,
    `Issue:       #${state.issueNumber ?? "N/A"}`,
    `Branch:      ${state.branch || "N/A"}`,
    `PR:          ${state.prNumber ? `#${state.prNumber}` : "N/A"}`,
    `Fix attempts: ${state.fixAttempts}/3`,
    `Review:      ${state.reviewApproved === null ? "pending" : state.reviewApproved ? "APPROVED" : "REJECTED"}`,
    `QA:          ${state.verifyPassed === null ? "pending" : state.verifyPassed ? "PASS" : "FAIL"}`,
    `Developer:   ${state.developerDone ? "done" : "working"}`,
    `Agents:      ${state.activeAgents.join(", ") || "none"}`,
    "",
    "Gates:",
  ];

  for (const [name, result] of Object.entries(state.gateResults)) {
    lines.push(`  ${name}: ${result.passed ? "PASS" : "FAIL"} (${(result.duration_ms / 1000).toFixed(1)}s)`);
  }

  if (state.eventLog.length > 0) {
    lines.push("", `Event log: ${state.eventLog.length} events (last: ${state.eventLog[state.eventLog.length - 1].op})`);
  }

  return { output: lines.join("\n"), exitCode: EXIT_ALLOW };
}

// ── Command dispatch ──

const COMMAND_HANDLERS: Record<string, CommandHandler> = {
  init: handleInit,
  transition: handleTransition,
  "run-gate": handleRunGate,
  "record-issue": handleRecordIssue,
  "record-branch": handleRecordBranch,
  "record-pr": handleRecordPr,
  "signal-done": handleSignalDone,
  "review-done": handleReviewDone,
  "verify-done": handleVerifyDone,
  status: handleStatus,
};

// ── Main ──

function main(): void {
  const args = process.argv.slice(2);

  let result: OperationResult;

  if (args.length === 0) {
    // Hook mode — read stdin
    result = runHookMode();
  } else {
    // Command mode
    const command = args[0];
    const handler = COMMAND_HANDLERS[command];
    if (!handler) {
      result = { output: `Unknown command: ${command}`, exitCode: EXIT_ERROR };
    } else {
      try {
        result = handler(args);
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : String(err);
        result = { output: msg, exitCode: EXIT_ERROR };
      }
    }
  }

  if (result.output) {
    process.stdout.write(result.output + "\n");
  }
  process.exit(result.exitCode);
}

main();
