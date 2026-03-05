import type { WorkflowDefinition, WorkflowContext, StepResult } from "../types.js";
import { shell } from "../util/shell.js";
import * as git from "../util/git.js";
import { implement } from "../steps/agent/implement.js";

const fetchIssue = {
  id: "fetch-issue",
  name: "Fetch Issue",
  kind: "deterministic" as const,
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    if (ctx.issueNumber == null) {
      return { status: "failed", output: "No issue number provided", duration_ms: Date.now() - start };
    }

    const { exitCode, stdout, stderr } = shell(
      `gh issue view ${ctx.issueNumber} --json number,title,body,labels`,
      { cwd: ctx.repoRoot },
    );

    if (exitCode !== 0) {
      return { status: "failed", output: `Failed to fetch issue: ${stderr}`, duration_ms: Date.now() - start };
    }

    return {
      status: "passed",
      output: stdout,
      artifacts: { issue: JSON.parse(stdout) },
      duration_ms: Date.now() - start,
    };
  },
};

const checkTree = {
  id: "check-tree",
  name: "Check Working Tree",
  kind: "deterministic" as const,
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    if (!git.isCleanTree(ctx.repoRoot)) {
      return { status: "failed", output: "Working tree is dirty", duration_ms: Date.now() - start };
    }
    return { status: "passed", output: "Clean working tree", duration_ms: Date.now() - start };
  },
};

const createBranch = {
  id: "create-branch",
  name: "Create Branch",
  kind: "deterministic" as const,
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();

    git.fetchOriginMain(ctx.repoRoot);

    const issueData = ctx.stepResults["fetch-issue"]?.artifacts?.issue as
      | { title?: string }
      | undefined;
    const title = issueData?.title ?? "feature";
    const slug = title
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-|-$/g, "")
      .slice(0, 50);

    const username = git.getUsername(ctx.repoRoot);
    const branchName = `${username}/${ctx.issueNumber}-${slug}`;

    if (!git.createBranch(ctx.repoRoot, branchName)) {
      return { status: "failed", output: `Failed to create branch: ${branchName}`, duration_ms: Date.now() - start };
    }

    ctx.branch = branchName;
    return {
      status: "passed",
      output: `Created branch: ${branchName}`,
      artifacts: { branchName },
      duration_ms: Date.now() - start,
    };
  },
};

export const startWorkWorkflow: WorkflowDefinition = {
  id: "start-work",
  maxRetries: 0,
  initialStep: "fetch-issue",
  steps: [fetchIssue, checkTree, createBranch, implement],
  transitions: [
    { from: "fetch-issue", to: "check-tree" },
    { from: "check-tree", to: "create-branch" },
    { from: "create-branch", to: "implement" },
  ],
};
