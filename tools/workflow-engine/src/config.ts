import { readFileSync } from "node:fs";
import { join } from "node:path";
import type { ProjectConfig } from "./types.js";

const CONFIG_FILENAME = "workflow.config.json";

const DEFAULT_CONFIG: ProjectConfig = {
  solution: "src/SignalBeam.sln",
  services: ["DeviceManager", "BundleOrchestrator", "TelemetryProcessor", "IdentityManager"],
  helmCharts: [
    "deploy/charts/signalbeam-infrastructure",
    "deploy/charts/signalbeam-platform",
  ],
  frontendDir: "web",
  infraDir: "infra",
};

export function loadConfig(repoRoot: string): ProjectConfig {
  const configPath = join(repoRoot, "tools", "workflow-engine", CONFIG_FILENAME);
  try {
    const raw = readFileSync(configPath, "utf-8");
    return { ...DEFAULT_CONFIG, ...JSON.parse(raw) } as ProjectConfig;
  } catch {
    return DEFAULT_CONFIG;
  }
}
