import { execSync } from "node:child_process";
import type { ShellResult } from "../types.js";

export interface ShellOptions {
  cwd?: string;
  timeout?: number;
  env?: Record<string, string>;
}

export function shell(
  command: string,
  options: ShellOptions = {},
): ShellResult {
  const { cwd, timeout = 120_000, env } = options;
  try {
    const stdout = execSync(command, {
      cwd,
      timeout,
      encoding: "utf-8",
      env: env ? { ...process.env, ...env } : undefined,
      stdio: ["pipe", "pipe", "pipe"],
      maxBuffer: 10 * 1024 * 1024,
    });
    return { exitCode: 0, stdout: stdout.trim(), stderr: "" };
  } catch (err: unknown) {
    const e = err as {
      status?: number;
      stdout?: string;
      stderr?: string;
      message?: string;
    };
    return {
      exitCode: e.status ?? 1,
      stdout: (e.stdout ?? "").toString().trim(),
      stderr: (e.stderr ?? e.message ?? "").toString().trim(),
    };
  }
}
