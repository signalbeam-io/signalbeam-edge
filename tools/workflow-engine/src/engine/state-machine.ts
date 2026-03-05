import type {
  StepResult,
  WorkflowContext,
  WorkflowDefinition,
  WorkflowStatus,
} from "../types.js";
import { executeParallel, executeStep } from "./executor.js";
import { loadState, saveState } from "../persistence/state-store.js";
import { log } from "../util/logger.js";

export interface RunResult {
  status: WorkflowStatus;
  stepResults: Record<string, StepResult>;
  failedStep?: string;
}

export async function runWorkflow(
  workflow: WorkflowDefinition,
  ctx: WorkflowContext,
): Promise<RunResult> {
  const stepMap = new Map(workflow.steps.map((s) => [s.id, s]));
  let currentStepId = ctx.currentStep || workflow.initialStep;

  log.header(`Workflow: ${workflow.id}`);

  while (true) {
    // Find current step(s)
    const transition = workflow.transitions.find((t) => t.from === currentStepId);

    // Execute current step
    const step = stepMap.get(currentStepId);
    if (!step) {
      log.error(`Step not found: ${currentStepId}`);
      return { status: "failed", stepResults: ctx.stepResults, failedStep: currentStepId };
    }

    const result = await executeStep(step, ctx);
    ctx.stepResults[currentStepId] = result;

    // Persist state after each step
    saveState(ctx.repoRoot, {
      version: 1,
      active: {
        workflowId: ctx.workflowId,
        context: ctx,
        status: "running",
        startedAt: loadState(ctx.repoRoot)?.active?.startedAt ?? new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
      history: loadState(ctx.repoRoot)?.history ?? [],
    });

    if (result.status === "failed") {
      // Check if step is retryable and we haven't exceeded max retries
      if (step.canRetry && ctx.retryCount < workflow.maxRetries) {
        log.warn(`Step ${currentStepId} failed, but is retryable (attempt ${ctx.retryCount + 1}/${workflow.maxRetries})`);
        ctx.retryCount++;
        continue;
      }

      log.error(`Step ${currentStepId} failed. Workflow halted.`);
      if (result.output) log.error(`  Output: ${result.output.slice(0, 500)}`);

      return { status: "failed", stepResults: ctx.stepResults, failedStep: currentStepId };
    }

    // No transition = we're done
    if (!transition) {
      log.header("Workflow Complete");
      return { status: "completed", stepResults: ctx.stepResults };
    }

    // Check guard
    if (transition.guard && !transition.guard(ctx)) {
      log.warn(`Guard blocked transition from ${currentStepId}`);
      return { status: "paused", stepResults: ctx.stepResults };
    }

    const next = transition.to;

    // Parallel execution
    if (Array.isArray(next)) {
      const parallelSteps = next
        .map((id) => stepMap.get(id))
        .filter((s): s is NonNullable<typeof s> => s != null);

      const parallelResults = await executeParallel(parallelSteps, ctx);
      Object.assign(ctx.stepResults, parallelResults);

      // Find the transition after the parallel group
      // Look for a transition whose `from` matches any of the parallel step IDs
      const afterParallel = workflow.transitions.find((t) =>
        next.includes(t.from),
      );

      if (!afterParallel) {
        log.header("Workflow Complete");
        return { status: "completed", stepResults: ctx.stepResults };
      }

      currentStepId = typeof afterParallel.to === "string"
        ? afterParallel.to
        : afterParallel.to[0];
    } else {
      currentStepId = next;
    }

    ctx.currentStep = currentStepId;
  }
}
