# Skill Writing Principles

Follow these principles when creating or modifying skills. This project's skills live in the cloudstack-ai-plugins marketplace (github.com/makigjuro/cloudstack-ai-plugins) — project-specific values belong in `cloudstack.json` or `.claude/rules/`, not in skill forks.

## Description Frontmatter

The `description` field in YAML frontmatter is the primary triggering mechanism — Claude decides whether to invoke a skill based on it. Write descriptions that are specific and slightly "pushy" to combat under-triggering.

**Good:** "Scaffold a new CQRS command with handler, validator, and POST endpoint following project conventions. Use this whenever the user wants to add a write operation, mutation endpoint, POST route, or state-changing action to any microservice."

**Bad:** "Create a command"

Include both what the skill does AND specific contexts/keywords that should trigger it.

## Writing Style

- Use imperative form in instructions
- Explain the **why** behind instructions, not just the what — Claude understands reasoning better than rigid rules
- Avoid heavy-handed MUSTs and NEVERs in all-caps unless genuinely critical (security, data loss). Instead, explain why something matters so the model understands the tradeoff
- Start with a draft, then review with fresh eyes for clarity and conciseness

## Structure

Keep SKILL.md under 500 lines. If approaching this limit, extract reference material into bundled files:

```
skill-name/
├── SKILL.md           # Core workflow (< 500 lines)
└── references/        # Detailed docs loaded on demand
    └── templates.md
```

## Progressive Disclosure

Skills load in three levels:
1. **Metadata** (name + description) — always in context (~100 words)
2. **SKILL.md body** — loaded when skill triggers
3. **Bundled resources** — loaded on demand via Read

Keep the most important instructions in the SKILL.md body. Put detailed templates, long examples, and reference tables in bundled files.

## Practical Patterns

- Include a checklist at the end for verification
- Add a "Related Skills" section linking to skills commonly used before/after
- Include realistic code templates grounded in existing project patterns
- If the skill produces output, show the exact format expected
- For skills that run bash commands, show the actual commands (not pseudocode)

## Avoiding Common Issues

- Don't duplicate instructions already in rules files — reference them instead ("Read `.claude/rules/infrastructure.md` for conventions")
- Don't over-constrain — give Claude room to adapt to the specific context
- Test with realistic prompts: would a user actually say this? Would the skill trigger?
- Keep examples concrete and tied to SignalBeam domain concepts, not abstract placeholders
