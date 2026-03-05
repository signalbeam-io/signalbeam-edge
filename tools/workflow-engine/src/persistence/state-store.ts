import { readFileSync, writeFileSync, renameSync, mkdirSync } from "node:fs";
import { join, dirname } from "node:path";
import type { StateFile } from "../types.js";

const STATE_FILENAME = ".workflow-state.json";

function statePath(repoRoot: string): string {
  return join(repoRoot, ".claude", STATE_FILENAME);
}

export function loadState(repoRoot: string): StateFile | null {
  try {
    const raw = readFileSync(statePath(repoRoot), "utf-8");
    return JSON.parse(raw) as StateFile;
  } catch {
    return null;
  }
}

export function saveState(repoRoot: string, state: StateFile): void {
  const path = statePath(repoRoot);
  const tmpPath = path + ".tmp";

  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(tmpPath, JSON.stringify(state, null, 2), "utf-8");
  renameSync(tmpPath, path);
}

export function clearActive(repoRoot: string): void {
  const state = loadState(repoRoot);
  if (!state) return;

  if (state.active) {
    state.history.push({
      workflowId: state.active.workflowId,
      branch: state.active.context.branch,
      status: state.active.status,
      completedAt: new Date().toISOString(),
    });
    state.active = null;
  }

  saveState(repoRoot, state);
}

export function initState(): StateFile {
  return { version: 1, active: null, history: [] };
}
