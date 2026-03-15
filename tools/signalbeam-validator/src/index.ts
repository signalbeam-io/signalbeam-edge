import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { execSync } from "child_process";
import * as fs from "fs";
import * as path from "path";

const REPO_ROOT = process.env.REPO_ROOT || process.cwd();

const server = new McpServer({
  name: "signalbeam-validator",
  version: "0.1.0",
});

// ─── Tool: detect_changes ────────────────────────────────────────────────────
// Analyzes git diff to determine which areas of the codebase changed.
// Returns structured flags used to gate downstream pipeline phases.

server.tool(
  "detect_changes",
  "Detect which areas of the codebase changed relative to origin/main. Returns flags for backend, frontend, infra, endpoints, entities, and events.",
  {},
  async () => {
    const files = run("git diff --name-only origin/main...HEAD");
    const lines = files.split("\n").filter(Boolean);

    const flags = {
      hasBackend: lines.some((f) => f.startsWith("src/")),
      hasFrontend: lines.some((f) => f.startsWith("web/")),
      hasInfra: lines.some(
        (f) =>
          f.startsWith("infra/") ||
          f.startsWith("deploy/") ||
          f.startsWith(".github/workflows/")
      ),
      hasEndpoints: lines.some((f) => f.includes("Endpoints/")),
      hasEntities: lines.some((f) => f.includes("Domain/Entities/")),
      hasEvents: lines.some((f) => f.includes("Domain/Events/")),
      hasTests: lines.some((f) => f.startsWith("tests/")),
      changedFiles: lines.length,
      files: lines,
    };

    return {
      content: [{ type: "text", text: JSON.stringify(flags, null, 2) }],
    };
  }
);

// ─── Tool: validate_layers ───────────────────────────────────────────────────
// Checks hexagonal architecture layer dependencies by parsing .csproj references.
// Domain must not reference Infrastructure or Host.
// Application must not reference Host.

const SERVICES = [
  "DeviceManager",
  "BundleOrchestrator",
  "TelemetryProcessor",
  "IdentityManager",
] as const;

server.tool(
  "validate_layers",
  "Check hexagonal architecture layer violations for a microservice. Verifies Domain has no deps on Infrastructure/Host, Application does not reference Host.",
  { service: z.enum(SERVICES).describe("Service name, e.g. DeviceManager") },
  async ({ service }) => {
    const violations: string[] = [];
    const servicePaths = findServicePaths(service);

    if (!servicePaths) {
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify({
              service,
              error: `Service ${service} not found`,
              passed: false,
            }),
          },
        ],
      };
    }

    // Check Domain layer (should have zero deps on Infrastructure or Host)
    const domainCsproj = readCsproj(servicePaths.domain);
    if (domainCsproj) {
      const refs = extractProjectRefs(domainCsproj);
      for (const ref of refs) {
        if (ref.includes("Infrastructure") || ref.includes("Host")) {
          violations.push(
            `Domain references ${path.basename(ref)} — Domain must have zero deps on Infrastructure/Host`
          );
        }
      }
    }

    // Check Application layer (should not reference Host)
    const appCsproj = readCsproj(servicePaths.application);
    if (appCsproj) {
      const refs = extractProjectRefs(appCsproj);
      for (const ref of refs) {
        if (ref.includes("Host")) {
          violations.push(
            `Application references ${path.basename(ref)} — Application must not reference Host`
          );
        }
      }
    }

    // Check for circular references
    const infraCsproj = readCsproj(servicePaths.infrastructure);
    if (infraCsproj) {
      const refs = extractProjectRefs(infraCsproj);
      for (const ref of refs) {
        if (ref.includes("Host")) {
          violations.push(
            `Infrastructure references ${path.basename(ref)} — Infrastructure must not reference Host`
          );
        }
      }
    }

    return {
      content: [
        {
          type: "text",
          text: JSON.stringify(
            {
              service,
              violations,
              passed: violations.length === 0,
              layersChecked: ["Domain", "Application", "Infrastructure"],
            },
            null,
            2
          ),
        },
      ],
    };
  }
);

// ─── Tool: check_pending_migrations ──────────────────────────────────────────
// Runs `dotnet ef migrations has-pending-model-changes` for a service.

server.tool(
  "check_pending_migrations",
  "Check if a microservice has EF Core model changes without a corresponding migration.",
  { service: z.enum(SERVICES).describe("Service name, e.g. DeviceManager") },
  async ({ service }) => {
    const servicePaths = findServicePaths(service);
    if (!servicePaths?.infrastructure || !servicePaths?.host) {
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify({
              service,
              pending: false,
              skipped: true,
              reason: "Infrastructure or Host project not found",
            }),
          },
        ],
      };
    }

    try {
      const output = run(
        `dotnet ef migrations has-pending-model-changes --project "${servicePaths.infrastructure}" --startup-project "${servicePaths.host}" 2>&1`
      );
      const hasPending =
        output.includes("pending") || output.includes("changes");
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(
              { service, pending: hasPending, output: output.trim() },
              null,
              2
            ),
          },
        ],
      };
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(
              { service, pending: false, error: msg },
              null,
              2
            ),
          },
        ],
      };
    }
  }
);

// ─── Tool: check_result_pattern ──────────────────────────────────────────────
// Scans handler files for thrown exceptions that should use Result<T> instead.

server.tool(
  "check_result_pattern",
  "Check if a C# handler file uses the Result pattern correctly — flags thrown business exceptions that should return Result<T> instead.",
  { filePath: z.string().describe("Absolute or repo-relative path to a .cs handler file") },
  async ({ filePath }) => {
    const absPath = path.isAbsolute(filePath)
      ? filePath
      : path.join(REPO_ROOT, filePath);

    if (!fs.existsSync(absPath)) {
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify({ file: filePath, error: "File not found" }),
          },
        ],
      };
    }

    const content = fs.readFileSync(absPath, "utf-8");
    const throwMatches = content.match(/throw new \w+Exception/g) || [];
    const usesResult = /Result<|Result\.Success|Result\.Failure/.test(content);
    const hasEmptyCatch = /catch\s*\([^)]*\)\s*\{\s*\}/.test(content);
    const catchesGenericException =
      /catch\s*\(\s*Exception\s/.test(content) &&
      !/throw;/.test(content);

    const issues: string[] = [];
    if (throwMatches.length > 0) {
      issues.push(
        `Throws ${throwMatches.length} business exception(s): ${throwMatches.join(", ")} — use Result<T> instead`
      );
    }
    if (hasEmptyCatch) {
      issues.push("Empty catch block detected — handle or propagate the error");
    }
    if (catchesGenericException) {
      issues.push(
        "Catches generic Exception without re-throw — catch specific exceptions or convert to Result"
      );
    }

    return {
      content: [
        {
          type: "text",
          text: JSON.stringify(
            {
              file: filePath,
              usesResultPattern: usesResult,
              thrownExceptions: throwMatches,
              issues,
              passed: issues.length === 0,
            },
            null,
            2
          ),
        },
      ],
    };
  }
);

// ─── Tool: validate_all_layers ───────────────────────────────────────────────
// Batch-validates all services at once.

server.tool(
  "validate_all_layers",
  "Check hexagonal architecture layer violations across all microservices in one call.",
  {},
  async () => {
    const results: Record<string, { violations: string[]; passed: boolean }> =
      {};

    for (const service of SERVICES) {
      const servicePaths = findServicePaths(service);
      if (!servicePaths) continue;

      const violations: string[] = [];

      const domainCsproj = readCsproj(servicePaths.domain);
      if (domainCsproj) {
        for (const ref of extractProjectRefs(domainCsproj)) {
          if (ref.includes("Infrastructure") || ref.includes("Host")) {
            violations.push(`Domain references ${path.basename(ref)}`);
          }
        }
      }

      const appCsproj = readCsproj(servicePaths.application);
      if (appCsproj) {
        for (const ref of extractProjectRefs(appCsproj)) {
          if (ref.includes("Host")) {
            violations.push(`Application references ${path.basename(ref)}`);
          }
        }
      }

      results[service] = { violations, passed: violations.length === 0 };
    }

    const allPassed = Object.values(results).every((r) => r.passed);

    return {
      content: [
        {
          type: "text",
          text: JSON.stringify({ results, allPassed }, null, 2),
        },
      ],
    };
  }
);

// ─── Tool: check_all_migrations ──────────────────────────────────────────────

server.tool(
  "check_all_migrations",
  "Check all microservices for pending EF Core migrations in one call.",
  {},
  async () => {
    const results: Record<
      string,
      { pending: boolean; skipped?: boolean; error?: string }
    > = {};

    for (const service of SERVICES) {
      const servicePaths = findServicePaths(service);
      if (!servicePaths?.infrastructure || !servicePaths?.host) {
        results[service] = {
          pending: false,
          skipped: true,
        };
        continue;
      }

      try {
        const output = run(
          `dotnet ef migrations has-pending-model-changes --project "${servicePaths.infrastructure}" --startup-project "${servicePaths.host}" 2>&1`
        );
        results[service] = {
          pending: output.includes("pending") || output.includes("changes"),
        };
      } catch (e) {
        results[service] = {
          pending: false,
          error: e instanceof Error ? e.message : String(e),
        };
      }
    }

    const anyPending = Object.values(results).some((r) => r.pending);

    return {
      content: [
        {
          type: "text",
          text: JSON.stringify({ results, anyPending }, null, 2),
        },
      ],
    };
  }
);

// ─── Helpers ─────────────────────────────────────────────────────────────────

function run(cmd: string): string {
  return execSync(cmd, { cwd: REPO_ROOT, encoding: "utf-8", timeout: 60_000 });
}

interface ServicePaths {
  domain?: string;
  application?: string;
  infrastructure?: string;
  host?: string;
}

function findServicePaths(service: string): ServicePaths | null {
  const srcDir = path.join(REPO_ROOT, "src");
  const paths: ServicePaths = {};

  // Services live either at src/{Service}/ or src/SignalBeam.{Service}/
  const prefixes = [service, `SignalBeam.${service}`];

  for (const prefix of prefixes) {
    const baseDir = path.join(srcDir, prefix);
    if (!fs.existsSync(baseDir)) continue;

    // Look for layer projects inside the service directory
    const entries = fs.readdirSync(baseDir, { withFileTypes: true });
    for (const entry of entries) {
      if (!entry.isDirectory()) continue;
      const name = entry.name;
      const csproj = findCsprojIn(path.join(baseDir, name));
      if (!csproj) continue;

      if (name.endsWith(".Domain") || name === "Domain") paths.domain = csproj;
      if (name.endsWith(".Application") || name === "Application")
        paths.application = csproj;
      if (name.endsWith(".Infrastructure") || name === "Infrastructure")
        paths.infrastructure = csproj;
      if (name.endsWith(".Host") || name === "Host") paths.host = csproj;
    }

    if (Object.keys(paths).length > 0) return paths;
  }

  // Flat structure: src/SignalBeam.{Service}.{Layer}/
  const srcEntries = fs.readdirSync(srcDir, { withFileTypes: true });
  for (const entry of srcEntries) {
    if (!entry.isDirectory()) continue;
    const name = entry.name;
    if (!name.includes(service)) continue;
    const csproj = findCsprojIn(path.join(srcDir, name));
    if (!csproj) continue;

    if (name.endsWith(".Domain")) paths.domain = csproj;
    if (name.endsWith(".Application")) paths.application = csproj;
    if (name.endsWith(".Infrastructure")) paths.infrastructure = csproj;
    if (name.endsWith(".Host")) paths.host = csproj;
  }

  return Object.keys(paths).length > 0 ? paths : null;
}

function findCsprojIn(dir: string): string | undefined {
  if (!fs.existsSync(dir)) return undefined;
  const files = fs.readdirSync(dir);
  const csproj = files.find((f) => f.endsWith(".csproj"));
  return csproj ? path.join(dir, csproj) : undefined;
}

function readCsproj(filePath?: string): string | null {
  if (!filePath || !fs.existsSync(filePath)) return null;
  return fs.readFileSync(filePath, "utf-8");
}

function extractProjectRefs(csprojContent: string): string[] {
  const matches = csprojContent.match(
    /Include="([^"]*\.csproj)"/g
  );
  if (!matches) return [];
  return matches.map((m) => m.replace(/Include="([^"]*)"/, "$1"));
}

// ─── Start server ────────────────────────────────────────────────────────────

const transport = new StdioServerTransport();
await server.connect(transport);
