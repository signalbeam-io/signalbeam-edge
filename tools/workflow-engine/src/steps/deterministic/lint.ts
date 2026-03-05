import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { shell } from "../../util/shell.js";
import { existsSync } from "node:fs";
import { join } from "node:path";

interface LintCheck {
  name: string;
  command: (ctx: WorkflowContext) => string;
  condition: (ctx: WorkflowContext) => boolean;
  timeout?: number;
}

function buildChecks(): LintCheck[] {
  return [
    {
      name: "dotnet format",
      command: (ctx) => `dotnet format ${ctx.config.solution} --verify-no-changes`,
      condition: (ctx) => ctx.changedFiles.some((f) => f.endsWith(".cs")),
      timeout: 120_000,
    },
    {
      name: "eslint",
      command: (ctx) => `cd ${ctx.config.frontendDir} && npm run lint`,
      condition: (ctx) =>
        ctx.changedFiles.some((f) => f.startsWith(ctx.config.frontendDir + "/")) &&
        existsSync(join(ctx.repoRoot, ctx.config.frontendDir, "node_modules")),
      timeout: 60_000,
    },
    {
      name: "type-check",
      command: (ctx) => `cd ${ctx.config.frontendDir} && npm run type-check`,
      condition: (ctx) =>
        ctx.changedFiles.some((f) => f.startsWith(ctx.config.frontendDir + "/")) &&
        existsSync(join(ctx.repoRoot, ctx.config.frontendDir, "node_modules")),
      timeout: 60_000,
    },
    {
      name: "terraform fmt",
      command: (ctx) => `terraform fmt -check -recursive ${ctx.config.infraDir}`,
      condition: (ctx) =>
        ctx.changedFiles.some((f) => f.startsWith(ctx.config.infraDir + "/")) &&
        shell("which terraform", { cwd: ctx.repoRoot }).exitCode === 0,
    },
  ];
}

function buildHelmChecks(ctx: WorkflowContext): LintCheck[] {
  if (!ctx.changedFiles.some((f) => f.startsWith("deploy/"))) return [];
  if (shell("which helm", { cwd: ctx.repoRoot }).exitCode !== 0) return [];

  return ctx.config.helmCharts.map((chart, i) => ({
    name: `helm lint (${chart.split("/").pop()})`,
    command: () => `helm lint ${chart}`,
    condition: () => true,
  }));
}

export const lint: StepDefinition = {
  id: "lint",
  name: "Lint & Format",
  kind: "deterministic",
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const failures: string[] = [];
    const passed: string[] = [];
    const skipped: string[] = [];

    // Auto-fix if requested
    if (ctx.args["auto-fix"]) {
      if (ctx.changedFiles.some((f) => f.endsWith(".cs"))) {
        shell(`dotnet format ${ctx.config.solution}`, { cwd: ctx.repoRoot, timeout: 120_000 });
      }
      if (ctx.changedFiles.some((f) => f.startsWith(ctx.config.frontendDir + "/"))) {
        shell(`cd ${ctx.config.frontendDir} && npm run lint:fix`, { cwd: ctx.repoRoot, timeout: 60_000 });
      }
    }

    const checks = [...buildChecks(), ...buildHelmChecks(ctx)];

    for (const check of checks) {
      if (!check.condition(ctx)) {
        skipped.push(check.name);
        continue;
      }

      const result = shell(check.command(ctx), {
        cwd: ctx.repoRoot,
        timeout: check.timeout ?? 60_000,
      });

      if (result.exitCode === 0) {
        passed.push(check.name);
      } else {
        failures.push(`${check.name}: ${result.stderr || result.stdout}`);
      }
    }

    const output = [
      `Passed: ${passed.join(", ") || "none"}`,
      skipped.length > 0 ? `Skipped: ${skipped.join(", ")}` : null,
      failures.length > 0 ? `Failed:\n${failures.join("\n")}` : null,
    ]
      .filter(Boolean)
      .join("\n");

    return {
      status: failures.length === 0 ? "passed" : "failed",
      output,
      artifacts: { passed, skipped, failures },
      duration_ms: Date.now() - start,
    };
  },
};
