import { shell, type ShellOptions } from "./shell.js";

function git(args: string, opts?: ShellOptions) {
  return shell(`git ${args}`, opts);
}

export function currentBranch(cwd: string): string {
  const { stdout } = git("branch --show-current", { cwd });
  return stdout;
}

export function isCleanTree(cwd: string): boolean {
  const { stdout } = git("status --porcelain", { cwd });
  return stdout === "";
}

export function isOnFeatureBranch(cwd: string): boolean {
  const branch = currentBranch(cwd);
  return branch !== "main" && branch !== "master" && branch !== "";
}

export function extractIssueNumber(branch: string): number | null {
  const match = branch.match(/\/(\d+)/);
  return match ? parseInt(match[1], 10) : null;
}

export function changedFiles(cwd: string, base = "origin/main"): string[] {
  const { exitCode, stdout } = git(`diff ${base}...HEAD --name-only`, { cwd });
  if (exitCode !== 0 || !stdout) return [];
  return stdout.split("\n").filter(Boolean);
}

export function diff(cwd: string, base = "origin/main"): string {
  const { stdout } = git(`diff ${base}...HEAD`, { cwd });
  return stdout;
}

export function commitLog(cwd: string, base = "origin/main"): string {
  const { stdout } = git(`log ${base}..HEAD --oneline`, { cwd });
  return stdout;
}

export function fetchOriginMain(cwd: string): boolean {
  const { exitCode } = git("fetch origin main", { cwd });
  return exitCode === 0;
}

export function createBranch(cwd: string, name: string): boolean {
  const { exitCode } = git(`checkout -b ${name} origin/main`, { cwd });
  return exitCode === 0;
}

export function getUsername(cwd: string): string {
  const { stdout } = git("config user.name", { cwd });
  return stdout.toLowerCase().replace(/\s+/g, "");
}
