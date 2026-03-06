// ── Result types ──

export type PreconditionResult =
  | { pass: true }
  | { pass: false; reason: string };

export function pass(): PreconditionResult {
  return { pass: true };
}

export function fail(reason: string): PreconditionResult {
  return { pass: false, reason };
}

// ── State machine ──

export type StateName =
  | "PREFLIGHT"
  | "BUILDING"
  | "LINTING"
  | "TESTING"
  | "REVIEWING"
  | "VERIFYING"
  | "FIXING"
  | "CREATING_PR"
  | "BLOCKED"
  | "COMPLETE";

export type StageType = "gate" | "agent" | "terminal";

export interface StateDefinition {
  emoji: string;
  stageType: StageType;
  /** Skill name from .claude/skills/ — loaded at runtime, not duplicated */
  skill: string | null;
  canTransitionTo: StateName[];
  allowedOperations: string[];
  transitionGuard: (ctx: GuardContext) => PreconditionResult;
  onEntry?: (state: WorkflowState, ctx: GuardContext) => WorkflowState;
}

export interface GuardContext {
  state: WorkflowState;
  config: ProjectConfig;
  gitInfo: GitInfo;
  from: StateName;
}

export interface GitInfo {
  branch: string;
  workingTreeClean: boolean;
  headCommit: string;
  hasCommitsVsDefault: boolean;
  changedFiles: string[];
}

// ── Workflow state (persisted) ──

export interface WorkflowState {
  state: StateName;
  issueNumber: number | null;
  branch: string;
  prNumber: number | null;
  fixAttempts: number;
  gateResults: Record<string, GateResult>;
  reviewApproved: boolean | null;
  reviewReport: string;
  verifyPassed: boolean | null;
  verifyReport: string;
  developerDone: boolean;
  activeAgents: string[];
  eventLog: WorkflowEvent[];
  args: Record<string, string | boolean>;
}

export interface GateResult {
  passed: boolean;
  output: string;
  duration_ms: number;
}

export interface WorkflowEvent {
  op: string;
  at: string;
  detail: Record<string, unknown>;
}

// ── Step result (reused by gate runners) ──

export interface StepResult {
  status: "passed" | "failed" | "skipped";
  output: string;
  artifacts?: Record<string, unknown>;
  duration_ms: number;
}

// ── Shell ──

export interface ShellResult {
  exitCode: number;
  stdout: string;
  stderr: string;
}

// ── Project config ──

export interface ProjectConfig {
  solution: string;
  services: string[];
  helmCharts: string[];
  frontendDir: string;
  infraDir: string;
}

// ── Hook I/O ──

export const EXIT_ALLOW = 0;
export const EXIT_BLOCK = 2;
export const EXIT_ERROR = 1;

export interface HookInput {
  hook_event_name: string;
  session_id: string;
  tool_name?: string;
  tool_input?: Record<string, unknown>;
  agent_name?: string;
  transcript_path?: string;
}

export interface OperationResult {
  output: string;
  exitCode: number;
}

// ── Skill (kept for rule injection) ──

export interface SkillDefinition {
  name: string;
  description: string;
  allowedTools: string[];
  userInvocable: boolean;
  body: string;
}
