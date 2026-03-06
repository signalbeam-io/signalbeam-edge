import type { WorkflowState, StepResult, ProjectConfig } from "../types.js";
import { shell } from "../util/shell.js";
import * as git from "../util/git.js";

type GateRunner = (state: WorkflowState, config: ProjectConfig, repoRoot: string) => StepResult;

function runPreflight(state: WorkflowState, _config: ProjectConfig, repoRoot: string): StepResult {
  const start = Date.now();
  const errors: string[] = [];

  if (!git.isOnFeatureBranch(repoRoot)) {
    errors.push(`Not on a feature branch (current: ${git.currentBranch(repoRoot) || "detached HEAD"})`);
  }
  if (!git.isCleanTree(repoRoot)) {
    errors.push("Working tree is dirty. Commit or stash changes first.");
  }

  return {
    status: errors.length === 0 ? "passed" : "failed",
    output: errors.length === 0 ? `Branch: ${state.branch}, Issue: #${state.issueNumber}` : errors.join("\n"),
    duration_ms: Date.now() - start,
  };
}

function runBuild(_state: WorkflowState, config: ProjectConfig, repoRoot: string): StepResult {
  const start = Date.now();

  // Check pending migrations
  for (const service of config.services) {
    const infraResult = shell(`find src -path "*/${service}*Infrastructure*.csproj" -print -quit`, { cwd: repoRoot });
    const hostResult = shell(`find src -path "*/${service}*Host*.csproj" -print -quit`, { cwd: repoRoot });
    if (infraResult.stdout && hostResult.stdout) {
      const { stdout, stderr } = shell(
        `dotnet ef migrations has-pending-model-changes --project "${infraResult.stdout}" --startup-project "${hostResult.stdout}"`,
        { cwd: repoRoot, timeout: 60_000 },
      );
      if ((stdout + stderr).includes("Changes have been made") || (stdout + stderr).includes("pending model changes")) {
        return {
          status: "failed",
          output: `Pending migrations in ${service}. Run /add-migration before proceeding.`,
          duration_ms: Date.now() - start,
        };
      }
    }
  }

  // Build
  const { exitCode, stdout, stderr } = shell(
    `dotnet build ${config.solution} --configuration Release`,
    { cwd: repoRoot, timeout: 180_000 },
  );

  return {
    status: exitCode === 0 ? "passed" : "failed",
    output: exitCode === 0 ? stdout : stderr || stdout,
    duration_ms: Date.now() - start,
  };
}

function runLint(state: WorkflowState, config: ProjectConfig, repoRoot: string): StepResult {
  const start = Date.now();
  const failures: string[] = [];
  const changed = git.changedFiles(repoRoot);

  if (changed.some((f) => f.endsWith(".cs"))) {
    const r = shell(`dotnet format ${config.solution} --verify-no-changes`, { cwd: repoRoot, timeout: 120_000 });
    if (r.exitCode !== 0) failures.push(`dotnet format: ${r.stderr || r.stdout}`);
  }

  if (changed.some((f) => f.startsWith(config.frontendDir + "/"))) {
    const r = shell(`cd ${config.frontendDir} && npm run lint`, { cwd: repoRoot, timeout: 60_000 });
    if (r.exitCode !== 0) failures.push(`eslint: ${r.stderr || r.stdout}`);
    const t = shell(`cd ${config.frontendDir} && npm run type-check`, { cwd: repoRoot, timeout: 60_000 });
    if (t.exitCode !== 0) failures.push(`type-check: ${t.stderr || t.stdout}`);
  }

  for (const chart of config.helmCharts) {
    if (changed.some((f) => f.startsWith("deploy/"))) {
      const r = shell(`helm lint ${chart}`, { cwd: repoRoot });
      if (r.exitCode !== 0) failures.push(`helm lint ${chart}: ${r.stderr}`);
    }
  }

  if (changed.some((f) => f.startsWith(config.infraDir + "/"))) {
    const r = shell(`terraform fmt -check -recursive ${config.infraDir}`, { cwd: repoRoot });
    if (r.exitCode !== 0) failures.push(`terraform fmt: ${r.stderr || r.stdout}`);
  }

  return {
    status: failures.length === 0 ? "passed" : "failed",
    output: failures.length === 0 ? "All linters passed" : failures.join("\n"),
    duration_ms: Date.now() - start,
  };
}

function runTest(state: WorkflowState, config: ProjectConfig, repoRoot: string): StepResult {
  const start = Date.now();

  // Unit tests
  const unit = shell(
    `dotnet test ${config.solution} --no-build --configuration Release --filter "Category!=Integration"`,
    { cwd: repoRoot, timeout: 300_000 },
  );
  if (unit.exitCode !== 0) {
    return { status: "failed", output: `Unit tests failed:\n${unit.stderr || unit.stdout}`, duration_ms: Date.now() - start };
  }

  // Integration tests (skip if flag set)
  if (!state.args["skip-integration"]) {
    const integ = shell(
      `dotnet test ${config.solution} --no-build --configuration Release --filter "Category=Integration"`,
      { cwd: repoRoot, timeout: 300_000 },
    );
    if (integ.exitCode !== 0) {
      return { status: "failed", output: `Integration tests failed:\n${integ.stderr || integ.stdout}`, duration_ms: Date.now() - start };
    }
  }

  return { status: "passed", output: "All tests passed", duration_ms: Date.now() - start };
}

const GATE_RUNNERS: Record<string, GateRunner> = {
  PREFLIGHT: runPreflight,
  BUILDING: runBuild,
  LINTING: runLint,
  TESTING: runTest,
};

export function runGate(
  gateName: string,
  state: WorkflowState,
  config: ProjectConfig,
  repoRoot: string,
): StepResult {
  const runner = GATE_RUNNERS[gateName];
  if (!runner) {
    return { status: "failed", output: `Unknown gate: ${gateName}`, duration_ms: 0 };
  }
  return runner(state, config, repoRoot);
}
