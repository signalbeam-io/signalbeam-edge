import type { StepDefinition, StepResult, WorkflowContext } from "../types.js";
import { log } from "../util/logger.js";

export async function executeStep(
  step: StepDefinition,
  ctx: WorkflowContext,
): Promise<StepResult> {
  log.step(step.id, "running");
  ctx.currentStep = step.id;

  const start = Date.now();
  try {
    const result = await step.run(ctx);
    log.step(step.id, result.status, result.duration_ms);
    return result;
  } catch (err: unknown) {
    const duration = Date.now() - start;
    const message = err instanceof Error ? err.message : String(err);
    log.step(step.id, "failed", duration);
    log.error(`  ${message}`);
    return {
      status: "failed",
      output: message,
      duration_ms: duration,
    };
  }
}

export async function executeParallel(
  steps: StepDefinition[],
  ctx: WorkflowContext,
): Promise<Record<string, StepResult>> {
  log.info(`Running ${steps.length} steps in parallel: ${steps.map((s) => s.id).join(", ")}`);

  const results = await Promise.all(
    steps.map(async (step) => {
      const result = await executeStep(step, ctx);
      return [step.id, result] as const;
    }),
  );

  return Object.fromEntries(results);
}
