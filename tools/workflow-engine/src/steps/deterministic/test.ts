import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { shell } from "../../util/shell.js";

function makeTestStep(
  id: string,
  name: string,
  filter: string,
  skipFlag?: string,
): StepDefinition {
  return {
    id,
    name,
    kind: "deterministic",
    canRetry: false,
    async run(ctx: WorkflowContext): Promise<StepResult> {
      if (skipFlag && ctx.args[skipFlag]) {
        return { status: "skipped", output: `Skipped via --${skipFlag}`, duration_ms: 0 };
      }

      const start = Date.now();
      const { exitCode, stdout, stderr } = shell(
        `dotnet test ${ctx.config.solution} --no-build --configuration Release --filter "${filter}"`,
        { cwd: ctx.repoRoot, timeout: 300_000 },
      );

      const testCountMatch = (stdout + stderr).match(/Total tests:\s*(\d+)/i) ??
        (stdout + stderr).match(/Passed:\s*(\d+)/i);
      const testCount = testCountMatch ? parseInt(testCountMatch[1], 10) : 0;

      return {
        status: exitCode === 0 ? "passed" : "failed",
        output: exitCode === 0 ? stdout : stderr || stdout,
        artifacts: { testCount },
        duration_ms: Date.now() - start,
      };
    },
  };
}

export const unitTests = makeTestStep(
  "unit-tests",
  "Unit Tests",
  "Category!=Integration",
);

export const integrationTests = makeTestStep(
  "integration-tests",
  "Integration Tests",
  "Category=Integration",
  "skip-integration",
);
