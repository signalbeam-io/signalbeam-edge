#!/usr/bin/env node

import { Command } from "commander";
import { resolve } from "node:path";
import { buildContext } from "./engine/context.js";
import { runWorkflow } from "./engine/state-machine.js";
import { getWorkflow, listWorkflows } from "./workflows/registry.js";
import { loadState, clearActive, initState, saveState } from "./persistence/state-store.js";
import { log } from "./util/logger.js";
import chalk from "chalk";

const program = new Command();

function getRepoRoot(): string {
  return resolve(process.env.REPO_ROOT ?? process.cwd());
}

program
  .name("sb-workflow")
  .description("Deterministic workflow orchestration for Claude agents")
  .version("0.1.0");

program
  .command("start-work <issue>")
  .description("Create feature branch and start implementation")
  .action(async (issue: string) => {
    const repoRoot = getRepoRoot();
    const ctx = buildContext("start-work", repoRoot, { issue });
    const workflow = getWorkflow("start-work")!;

    saveState(repoRoot, {
      ...initState(),
      active: {
        workflowId: "start-work",
        context: ctx,
        status: "running",
        startedAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
    });

    const result = await runWorkflow(workflow, ctx);

    if (result.status === "completed") {
      clearActive(repoRoot);
      log.info(chalk.green("Branch created and implementation started."));
    } else {
      log.error(`Workflow ${result.status}${result.failedStep ? ` at step: ${result.failedStep}` : ""}`);
      process.exitCode = 1;
    }
  });

program
  .command("complete-task")
  .description("Run full standard quality pipeline")
  .option("--skip-integration", "Skip integration tests")
  .option("--auto-fix", "Auto-fix lint issues")
  .option("--issue <number>", "GitHub issue number")
  .action(async (opts: { skipIntegration?: boolean; autoFix?: boolean; issue?: string }) => {
    const repoRoot = getRepoRoot();
    const args: Record<string, string | boolean> = {};
    if (opts.skipIntegration) args["skip-integration"] = true;
    if (opts.autoFix) args["auto-fix"] = true;
    if (opts.issue) args["issue"] = opts.issue;

    const ctx = buildContext("complete-task", repoRoot, args);
    const workflow = getWorkflow("complete-task")!;

    saveState(repoRoot, {
      ...initState(),
      active: {
        workflowId: "complete-task",
        context: ctx,
        status: "running",
        startedAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
    });

    const result = await runWorkflow(workflow, ctx);

    if (result.status === "completed") {
      clearActive(repoRoot);
      const prUrl = (result.stepResults["create-pr"]?.artifacts as Record<string, unknown>)?.prUrl;
      log.info(chalk.green(`Task completed. PR: ${prUrl ?? "N/A"}`));
    } else {
      log.error(`Workflow ${result.status}${result.failedStep ? ` at step: ${result.failedStep}` : ""}`);
      process.exitCode = 1;
    }
  });

program
  .command("complete-infra")
  .description("Run infrastructure quality pipeline")
  .option("--issue <number>", "GitHub issue number")
  .action(async (opts: { issue?: string }) => {
    const repoRoot = getRepoRoot();
    const args: Record<string, string | boolean> = {};
    if (opts.issue) args["issue"] = opts.issue;

    const ctx = buildContext("complete-infra", repoRoot, args);
    const workflow = getWorkflow("complete-infra")!;

    saveState(repoRoot, {
      ...initState(),
      active: {
        workflowId: "complete-infra",
        context: ctx,
        status: "running",
        startedAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
    });

    const result = await runWorkflow(workflow, ctx);

    if (result.status === "completed") {
      clearActive(repoRoot);
      log.info(chalk.green("Infrastructure task completed."));
    } else {
      log.error(`Workflow ${result.status}${result.failedStep ? ` at step: ${result.failedStep}` : ""}`);
      process.exitCode = 1;
    }
  });

program
  .command("resume")
  .description("Resume from last saved state")
  .action(async () => {
    const repoRoot = getRepoRoot();
    const state = loadState(repoRoot);

    if (!state?.active) {
      log.info("No active workflow to resume.");
      return;
    }

    const { workflowId, context } = state.active;
    log.info(`Resuming workflow: ${workflowId} from step: ${context.currentStep}`);

    const workflow = getWorkflow(workflowId);
    if (!workflow) {
      log.error(`Unknown workflow: ${workflowId}`);
      process.exitCode = 1;
      return;
    }

    const result = await runWorkflow(workflow, context);

    if (result.status === "completed") {
      clearActive(repoRoot);
      log.info(chalk.green("Workflow completed."));
    } else {
      log.error(`Workflow ${result.status}${result.failedStep ? ` at step: ${result.failedStep}` : ""}`);
      process.exitCode = 1;
    }
  });

program
  .command("status")
  .description("Show current workflow state")
  .action(() => {
    const repoRoot = getRepoRoot();
    const state = loadState(repoRoot);

    if (!state?.active) {
      log.info("No active workflow.");
    } else {
      const { workflowId, context, status, startedAt } = state.active;
      log.header(`Active Workflow: ${workflowId}`);
      console.log(`  Status:  ${status}`);
      console.log(`  Branch:  ${context.branch}`);
      console.log(`  Issue:   #${context.issueNumber ?? "N/A"}`);
      console.log(`  Step:    ${context.currentStep}`);
      console.log(`  Retries: ${context.retryCount}`);
      console.log(`  Started: ${startedAt}`);
      log.divider();

      const stepIds = Object.keys(context.stepResults);
      if (stepIds.length > 0) {
        console.log("  Steps:");
        for (const id of stepIds) {
          const r = context.stepResults[id];
          log.step(id, r.status, r.duration_ms);
        }
      }
    }

    if (state?.history && state.history.length > 0) {
      log.header("History");
      for (const h of state.history.slice(-5)) {
        console.log(`  ${h.workflowId} on ${h.branch} — ${h.status} (${h.completedAt})`);
      }
    }
  });

program
  .command("list")
  .description("List available workflows")
  .action(() => {
    log.header("Available Workflows");
    for (const id of listWorkflows()) {
      console.log(`  ${id}`);
    }
  });

program.parse();
