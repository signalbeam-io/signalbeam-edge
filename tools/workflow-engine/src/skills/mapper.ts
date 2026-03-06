import type { SkillDefinition } from "../types.js";

/**
 * Build the system prompt from a skill definition.
 *
 * Only returns the skill body. Rules are NOT injected here —
 * Claude Code handles rule injection natively via `paths:` frontmatter
 * in `.claude/rules/*.md`. Adding them here would duplicate context.
 */
export function buildSystemPrompt(skill: SkillDefinition): string {
  return skill.body;
}
