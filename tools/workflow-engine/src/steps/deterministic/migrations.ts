import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { shell } from "../../util/shell.js";
import { readdirSync } from "node:fs";
import { join } from "node:path";

function findProject(repoRoot: string, service: string, suffix: string): string | null {
  const srcDir = join(repoRoot, "src");
  try {
    const entries = readdirSync(srcDir, { withFileTypes: true, recursive: true });
    for (const entry of entries) {
      if (
        entry.isFile() &&
        entry.name.endsWith(".csproj") &&
        entry.name.includes(service) &&
        entry.name.includes(suffix)
      ) {
        const parentPath = (entry as any).parentPath ?? (entry as any).path;
        return join(parentPath, entry.name);
      }
    }
  } catch {
    // ignore
  }
  return null;
}

export const migrations: StepDefinition = {
  id: "migrations",
  name: "Pending Migrations Check",
  kind: "deterministic",
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const pending: string[] = [];
    const checked: string[] = [];

    for (const service of ctx.config.services) {
      const infra = findProject(ctx.repoRoot, service, "Infrastructure");
      const host = findProject(ctx.repoRoot, service, "Host");

      if (!infra || !host) continue;

      checked.push(service);
      const { stdout, stderr } = shell(
        `dotnet ef migrations has-pending-model-changes --project "${infra}" --startup-project "${host}"`,
        { cwd: ctx.repoRoot, timeout: 60_000 },
      );

      const output = stdout + stderr;
      if (output.includes("Changes have been made") || output.includes("pending model changes")) {
        pending.push(service);
      }
    }

    if (pending.length > 0) {
      return {
        status: "failed",
        output: `Pending migrations in: ${pending.join(", ")}. Run /add-migration before proceeding.`,
        artifacts: { pending, checked },
        duration_ms: Date.now() - start,
      };
    }

    return {
      status: "passed",
      output: `Checked ${checked.length} services, no pending migrations.`,
      artifacts: { checked },
      duration_ms: Date.now() - start,
    };
  },
};
