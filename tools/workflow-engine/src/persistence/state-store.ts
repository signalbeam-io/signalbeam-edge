import { readFileSync, writeFileSync, renameSync, mkdirSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import type { WorkflowState, WorkflowEvent } from "../types.js";

function statePath(sessionId: string): string {
  return `/tmp/workflow-engine-state-${sessionId}.json`;
}

export function createInitialState(): WorkflowState {
  return {
    state: "PREFLIGHT",
    issueNumber: null,
    branch: "",
    prNumber: null,
    fixAttempts: 0,
    gateResults: {},
    reviewApproved: null,
    reviewReport: "",
    verifyPassed: null,
    verifyReport: "",
    developerDone: false,
    activeAgents: [],
    eventLog: [],
    args: {},
  };
}

export function sessionExists(sessionId: string): boolean {
  return existsSync(statePath(sessionId));
}

export function loadWorkflowState(sessionId: string): WorkflowState | null {
  try {
    const raw = readFileSync(statePath(sessionId), "utf-8");
    return JSON.parse(raw) as WorkflowState;
  } catch {
    return null;
  }
}

export function saveWorkflowState(sessionId: string, state: WorkflowState): void {
  const path = statePath(sessionId);
  const tmpPath = path + ".tmp";
  writeFileSync(tmpPath, JSON.stringify(state, null, 2), "utf-8");
  renameSync(tmpPath, path);
}

export function appendEvent(
  state: WorkflowState,
  op: string,
  detail: Record<string, unknown> = {},
): WorkflowState {
  const event: WorkflowEvent = {
    op,
    at: new Date().toISOString(),
    detail,
  };
  return {
    ...state,
    eventLog: [...state.eventLog, event],
  };
}
