import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { shell } from "../../util/shell.js";
import { log } from "../../util/logger.js";

export const createPr: StepDefinition = {
  id: "create-pr",
  name: "Create Pull Request",
  kind: "agent",
  skill: "create-pr",
  tools: ["Bash"],
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();

    // Push branch
    log.info(`Pushing branch ${ctx.branch}...`);
    const push = shell(`git push -u origin ${ctx.branch}`, { cwd: ctx.repoRoot });
    if (push.exitCode !== 0) {
      return {
        status: "failed",
        output: `Failed to push: ${push.stderr}`,
        duration_ms: Date.now() - start,
      };
    }

    // Build PR body from step results
    const gates = Object.entries(ctx.stepResults)
      .filter(([id]) => id !== "create-pr")
      .map(([id, r]) => `- ${id}: ${r.status.toUpperCase()}`)
      .join("\n");

    const issueRef = ctx.issueNumber ? `Closes #${ctx.issueNumber}` : "";
    const body = [
      "## Summary",
      "",
      issueRef,
      "",
      "## Quality Gates",
      "",
      gates,
      "",
      "## Test plan",
      "",
      "- [ ] All deterministic gates passed",
      "- [ ] Code review approved",
      "- [ ] QA verification passed",
      "",
      "Generated with [sb-workflow](https://github.com/signalbeam-io/signalbeam-edge)",
    ].join("\n");

    const title = ctx.branch.replace(/^[^/]+\/\d+-/, "").replace(/-/g, " ");

    const pr = shell(
      `gh pr create --title "${title}" --body "$(cat <<'PREOF'\n${body}\nPREOF\n)"`,
      { cwd: ctx.repoRoot },
    );

    if (pr.exitCode !== 0) {
      return {
        status: "failed",
        output: `Failed to create PR: ${pr.stderr || pr.stdout}`,
        duration_ms: Date.now() - start,
      };
    }

    const prUrl = pr.stdout.trim();
    log.info(`PR created: ${prUrl}`);

    return {
      status: "passed",
      output: prUrl,
      artifacts: { prUrl },
      duration_ms: Date.now() - start,
    };
  },
};
