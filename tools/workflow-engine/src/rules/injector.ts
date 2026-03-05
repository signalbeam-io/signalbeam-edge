import { readFileSync } from "node:fs";
import { join } from "node:path";
import { minimatch } from "minimatch";

const RULES_DIR = ".claude/rules";

const RULE_MAP: [string, string][] = [
  ["src/**/*.cs", "csharp-general.md"],
  ["src/**/Entities/**", "domain-layer.md"],
  ["src/**/Endpoints/**", "endpoints.md"],
  ["src/**/Validators/**", "validators.md"],
  ["web/**", "react-frontend.md"],
  ["deploy/charts/**", "helm-charts.md"],
  ["infra/**", "infrastructure.md"],
  ["tests/**", "testing.md"],
];

const ALWAYS_INCLUDE = ["security.md", "git-conventions.md"];

export function selectRules(changedFiles: string[]): string[] {
  const matched = new Set<string>(ALWAYS_INCLUDE);

  for (const file of changedFiles) {
    for (const [pattern, rule] of RULE_MAP) {
      if (minimatch(file, pattern)) {
        matched.add(rule);
      }
    }
  }

  return [...matched];
}

export function injectRules(repoRoot: string, changedFiles: string[]): string[] {
  const ruleNames = selectRules(changedFiles);
  const contents: string[] = [];

  for (const name of ruleNames) {
    try {
      const content = readFileSync(join(repoRoot, RULES_DIR, name), "utf-8");
      contents.push(content.trim());
    } catch {
      // Rule file not found, skip
    }
  }

  return contents;
}
