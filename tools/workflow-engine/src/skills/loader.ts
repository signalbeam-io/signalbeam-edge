import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import type { SkillDefinition } from "../types.js";

const SKILL_DIR = ".claude/skills";

export function loadSkill(repoRoot: string, skillName: string): SkillDefinition | null {
  const skillPath = join(repoRoot, SKILL_DIR, skillName, "SKILL.md");

  try {
    const raw = readFileSync(skillPath, "utf-8");
    return parseSkillFile(raw);
  } catch {
    return null;
  }
}

export function listSkills(repoRoot: string): string[] {
  try {
    return readdirSync(join(repoRoot, SKILL_DIR), { withFileTypes: true })
      .filter((d) => d.isDirectory())
      .map((d) => d.name);
  } catch {
    return [];
  }
}

function parseSkillFile(content: string): SkillDefinition {
  const frontmatterMatch = content.match(/^---\n([\s\S]*?)\n---\n([\s\S]*)$/);

  if (!frontmatterMatch) {
    return {
      name: "unknown",
      description: "",
      allowedTools: [],
      userInvocable: false,
      body: content,
    };
  }

  const frontmatter = frontmatterMatch[1];
  const body = frontmatterMatch[2].trim();

  const name = extractField(frontmatter, "name") ?? "unknown";
  const description = extractField(frontmatter, "description") ?? "";
  const allowedTools = (extractField(frontmatter, "allowed-tools") ?? "")
    .split(",")
    .map((t) => t.trim())
    .filter(Boolean);
  const userInvocable = extractField(frontmatter, "user-invocable") === "true";

  return { name, description, allowedTools, userInvocable, body };
}

function extractField(frontmatter: string, field: string): string | null {
  const match = frontmatter.match(new RegExp(`^${field}:\\s*(.+)$`, "m"));
  return match ? match[1].trim() : null;
}
