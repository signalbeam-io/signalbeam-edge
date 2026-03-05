import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { shell } from "../../util/shell.js";

export const build: StepDefinition = {
  id: "build",
  name: "Build Solution",
  kind: "deterministic",
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const { exitCode, stdout, stderr } = shell(
      `dotnet build ${ctx.config.solution} --configuration Release`,
      { cwd: ctx.repoRoot, timeout: 180_000 },
    );

    return {
      status: exitCode === 0 ? "passed" : "failed",
      output: exitCode === 0 ? stdout : stderr || stdout,
      duration_ms: Date.now() - start,
    };
  },
};
