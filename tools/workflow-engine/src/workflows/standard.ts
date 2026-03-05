import type { WorkflowDefinition } from "../types.js";
import { preflight } from "../steps/deterministic/preflight.js";
import { migrations } from "../steps/deterministic/migrations.js";
import { build } from "../steps/deterministic/build.js";
import { lint } from "../steps/deterministic/lint.js";
import { unitTests, integrationTests } from "../steps/deterministic/test.js";
import { review } from "../steps/agent/review.js";
import { verify } from "../steps/agent/verify.js";
import { fix } from "../steps/agent/fix.js";
import { createPr } from "../steps/agent/create-pr.js";

const evaluate = {
  id: "evaluate",
  name: "Evaluate Review + QA",
  kind: "deterministic" as const,
  canRetry: false,
  async run(ctx: import("../types.js").WorkflowContext) {
    const start = Date.now();
    const reviewResult = ctx.stepResults["review"];
    const verifyResult = ctx.stepResults["verify"];

    const reviewPassed = reviewResult?.status === "passed";
    const verifyPassed = verifyResult?.status === "passed";

    if (reviewPassed && verifyPassed) {
      return { status: "passed" as const, output: "Both review and QA passed", duration_ms: Date.now() - start };
    }

    const issues: string[] = [];
    if (!reviewPassed) issues.push("Code review: CHANGES REQUESTED");
    if (!verifyPassed) issues.push("QA verification: FAIL");

    return {
      status: "failed" as const,
      output: issues.join("\n"),
      duration_ms: Date.now() - start,
    };
  },
};

export const standardWorkflow: WorkflowDefinition = {
  id: "complete-task",
  maxRetries: 3,
  initialStep: "preflight",
  steps: [preflight, migrations, build, lint, unitTests, integrationTests, review, verify, evaluate, fix, createPr],
  transitions: [
    { from: "preflight", to: "migrations" },
    { from: "migrations", to: "build" },
    { from: "build", to: "lint" },
    { from: "lint", to: "unit-tests" },
    { from: "unit-tests", to: "integration-tests" },
    { from: "integration-tests", to: ["review", "verify"] },
    // After parallel review+verify, evaluate results
    { from: "review", to: "evaluate" },
    // evaluate passes -> create-pr; fails -> fix
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
    // fix loops back to build
    { from: "fix", to: "build" },
    // create-pr is terminal (no transition = done)
  ],
};
