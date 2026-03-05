import type { WorkflowDefinition, WorkflowContext, StepResult } from "../types.js";
import { preflight } from "../steps/deterministic/preflight.js";
import { shell } from "../util/shell.js";
import { review } from "../steps/agent/review.js";
import { fix } from "../steps/agent/fix.js";
import { createPr } from "../steps/agent/create-pr.js";

const tfFmt = {
  id: "tf-fmt",
  name: "Terraform Format",
  kind: "deterministic" as const,
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const { exitCode, stdout, stderr } = shell(
      `terraform fmt -check -recursive ${ctx.config.infraDir}`,
      { cwd: ctx.repoRoot },
    );
    return {
      status: exitCode === 0 ? "passed" : "failed",
      output: exitCode === 0 ? "Terraform format OK" : stderr || stdout,
      duration_ms: Date.now() - start,
    };
  },
};

const tfValidate = {
  id: "tf-validate",
  name: "Terraform Validate",
  kind: "deterministic" as const,
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const { exitCode, stdout, stderr } = shell(
      `terraform validate`,
      { cwd: ctx.repoRoot },
    );
    return {
      status: exitCode === 0 ? "passed" : "failed",
      output: exitCode === 0 ? "Terraform valid" : stderr || stdout,
      duration_ms: Date.now() - start,
    };
  },
};

const helmLint = {
  id: "helm-lint",
  name: "Helm Lint",
  kind: "deterministic" as const,
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const failures: string[] = [];

    for (const chart of ctx.config.helmCharts) {
      const { exitCode, stderr } = shell(`helm lint ${chart}`, { cwd: ctx.repoRoot });
      if (exitCode !== 0) failures.push(`${chart}: ${stderr}`);
    }

    return {
      status: failures.length === 0 ? "passed" : "failed",
      output: failures.length === 0 ? "All charts pass" : failures.join("\n"),
      duration_ms: Date.now() - start,
    };
  },
};

const evaluate = {
  id: "evaluate",
  name: "Evaluate Review",
  kind: "deterministic" as const,
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const reviewResult = ctx.stepResults["review"];
    if (reviewResult?.status === "passed") {
      return { status: "passed", output: "Review passed", duration_ms: Date.now() - start };
    }
    return { status: "failed", output: "Review: CHANGES REQUESTED", duration_ms: Date.now() - start };
  },
};

export const infraWorkflow: WorkflowDefinition = {
  id: "complete-infra",
  maxRetries: 3,
  initialStep: "preflight",
  steps: [preflight, tfFmt, tfValidate, helmLint, review, evaluate, fix, createPr],
  transitions: [
    { from: "preflight", to: "tf-fmt" },
    { from: "tf-fmt", to: "tf-validate" },
    { from: "tf-validate", to: "helm-lint" },
    { from: "helm-lint", to: "review" },
    { from: "review", to: "evaluate" },
    {
      from: "evaluate",
      to: "create-pr",
      guard: (ctx) => ctx.stepResults["evaluate"]?.status === "passed",
    },
    {
      from: "evaluate",
      to: "fix",
      guard: (ctx) => ctx.stepResults["evaluate"]?.status === "failed",
    },
    { from: "fix", to: "tf-fmt" },
  ],
};
