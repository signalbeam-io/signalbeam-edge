import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import * as git from "../../util/git.js";

export const preflight: StepDefinition = {
  id: "preflight",
  name: "Pre-flight Checks",
  kind: "deterministic",
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const errors: string[] = [];

    if (!git.isOnFeatureBranch(ctx.repoRoot)) {
      errors.push(`Not on a feature branch (current: ${ctx.branch || "detached HEAD"})`);
    }

    if (!git.isCleanTree(ctx.repoRoot)) {
      errors.push("Working tree is dirty. Commit or stash changes first.");
    }

    if (ctx.issueNumber == null) {
      errors.push("Could not extract issue number from branch name. Pass --issue <number>.");
    }

    if (errors.length > 0) {
      return {
        status: "failed",
        output: errors.join("\n"),
        duration_ms: Date.now() - start,
      };
    }

    return {
      status: "passed",
      output: `Branch: ${ctx.branch}, Issue: #${ctx.issueNumber}`,
      artifacts: { branch: ctx.branch, issueNumber: ctx.issueNumber },
      duration_ms: Date.now() - start,
    };
  },
};
