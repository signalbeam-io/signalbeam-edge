import type { WorkflowContext } from "../types.js";
import { loadConfig } from "../config.js";
import * as git from "../util/git.js";

export function buildContext(
  workflowId: string,
  repoRoot: string,
  args: Record<string, string | boolean> = {},
): WorkflowContext {
  const branch = git.currentBranch(repoRoot);
  const issueNumber =
    typeof args.issue === "string"
      ? parseInt(args.issue, 10)
      : git.extractIssueNumber(branch);

  return {
    workflowId,
    issueNumber,
    branch,
    repoRoot,
    currentStep: "",
    retryCount: 0,
    stepResults: {},
    changedFiles: git.changedFiles(repoRoot),
    args,
    config: loadConfig(repoRoot),
  };
}
