import type { HookInput, OperationResult } from "../types.js";
import { createInitialState, saveWorkflowState, sessionExists } from "../persistence/state-store.js";
import { allow, injectContext } from "./hook-io.js";
import { readFileSync } from "node:fs";

export function handleSessionStart(input: HookInput, pluginRoot: string): OperationResult {
  if (sessionExists(input.session_id)) {
    return allow();
  }

  // Don't auto-create state — only /start-feature does that
  return allow();
}
