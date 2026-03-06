import type { HookInput, OperationResult } from "../types.js";
import { EXIT_ALLOW, EXIT_BLOCK } from "../types.js";

export function parseStdin(): HookInput {
  const raw = process.stdin.isTTY ? "{}" : require("fs").readFileSync(0, "utf-8");
  try {
    return JSON.parse(raw) as HookInput;
  } catch {
    return { hook_event_name: "", session_id: "" };
  }
}

export function allow(output = ""): OperationResult {
  return { output, exitCode: EXIT_ALLOW };
}

export function block(reason: string): OperationResult {
  return {
    output: JSON.stringify({ decision: "block", reason }),
    exitCode: EXIT_BLOCK,
  };
}

export function injectContext(content: string): OperationResult {
  return {
    output: JSON.stringify({ decision: "allow", additionalContext: content }),
    exitCode: EXIT_ALLOW,
  };
}
