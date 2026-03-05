import type { SkillDefinition } from "../types.js";
import { injectRules } from "../rules/injector.js";

export function buildSystemPrompt(
  repoRoot: string,
  skill: SkillDefinition,
  changedFiles: string[],
): string {
  const rules = injectRules(repoRoot, changedFiles);
  const parts: string[] = [];

  parts.push(skill.body);

  if (rules.length > 0) {
    parts.push("---\n\n# Applicable Rules\n");
    parts.push(rules.join("\n\n---\n\n"));
  }

  return parts.join("\n\n");
}
