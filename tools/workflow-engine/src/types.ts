export type StepKind = "deterministic" | "agent";
export type StepStatus = "pending" | "running" | "passed" | "failed" | "skipped";
export type WorkflowStatus = "idle" | "running" | "completed" | "failed" | "paused";

export interface StepResult {
  status: "passed" | "failed" | "skipped";
  output: string;
  artifacts?: Record<string, unknown>;
  duration_ms: number;
}

export interface StepDefinition {
  id: string;
  name: string;
  kind: StepKind;
  skill?: string;
  tools?: string[];
  run: (ctx: WorkflowContext) => Promise<StepResult>;
  canRetry: boolean;
  dependsOn?: string[];
}

export interface Transition {
  from: string;
  to: string | string[];
  guard?: (ctx: WorkflowContext) => boolean;
}

export interface WorkflowDefinition {
  id: string;
  steps: StepDefinition[];
  transitions: Transition[];
  initialStep: string;
  maxRetries: number;
}

export interface WorkflowContext {
  workflowId: string;
  issueNumber: number | null;
  branch: string;
  repoRoot: string;
  currentStep: string;
  retryCount: number;
  stepResults: Record<string, StepResult>;
  changedFiles: string[];
  args: Record<string, string | boolean>;
  config: ProjectConfig;
}

export interface StateFile {
  version: 1;
  active: {
    workflowId: string;
    context: WorkflowContext;
    status: WorkflowStatus;
    startedAt: string;
    updatedAt: string;
  } | null;
  history: Array<{
    workflowId: string;
    branch: string;
    status: string;
    completedAt: string;
  }>;
}

export interface SkillDefinition {
  name: string;
  description: string;
  allowedTools: string[];
  userInvocable: boolean;
  body: string;
}

export interface ShellResult {
  exitCode: number;
  stdout: string;
  stderr: string;
}

export interface ProjectConfig {
  /** Path to the .sln file relative to repo root (e.g. "src/MyApp.sln") */
  solution: string;
  /** Service names that have EF Core DbContexts for migration checks */
  services: string[];
  /** Helm chart paths relative to repo root */
  helmCharts: string[];
  /** Frontend directory relative to repo root (e.g. "web") */
  frontendDir: string;
  /** Infrastructure directory relative to repo root (e.g. "infra") */
  infraDir: string;
}
