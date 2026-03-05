import Anthropic from "@anthropic-ai/sdk";
import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { loadSkill } from "../../skills/loader.js";
import { buildSystemPrompt } from "../../skills/mapper.js";
import * as git from "../../util/git.js";
import { log } from "../../util/logger.js";

export const review: StepDefinition = {
  id: "review",
  name: "Code Review",
  kind: "agent",
  skill: "code-review",
  tools: ["Read", "Glob", "Grep"],
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const skill = loadSkill(ctx.repoRoot, "code-review");

    if (!skill) {
      return {
        status: "failed",
        output: "Could not load code-review skill",
        duration_ms: Date.now() - start,
      };
    }

    const systemPrompt = buildSystemPrompt(ctx.repoRoot, skill, ctx.changedFiles);
    const diffOutput = git.diff(ctx.repoRoot);
    const commitHistory = git.commitLog(ctx.repoRoot);
    const fileList = ctx.changedFiles.join("\n");

    const client = new Anthropic();

    try {
      const response = await client.messages.create({
        model: "claude-sonnet-4-20250514",
        max_tokens: 16384,
        system: systemPrompt,
        messages: [
          {
            role: "user",
            content: [
              `Review the following changes on branch \`${ctx.branch}\`.\n`,
              `## Changed Files\n\`\`\`\n${fileList}\n\`\`\`\n`,
              `## Commits\n\`\`\`\n${commitHistory}\n\`\`\`\n`,
              `## Diff\n\`\`\`diff\n${diffOutput.slice(0, 100_000)}\n\`\`\`\n`,
              "Return your review. End with **Overall: APPROVED** or **Overall: CHANGES REQUESTED**.",
            ].join("\n"),
          },
        ],
      });

      const text = response.content
        .filter((b): b is Anthropic.TextBlock => b.type === "text")
        .map((b) => b.text)
        .join("\n");

      const approved = /overall:\s*approved/i.test(text);
      const status = approved ? "passed" : "failed";

      log.info(`Code review: ${approved ? "APPROVED" : "CHANGES REQUESTED"}`);

      return {
        status,
        output: text,
        artifacts: { approved, reviewText: text },
        duration_ms: Date.now() - start,
      };
    } catch (err: unknown) {
      return {
        status: "failed",
        output: `API error: ${err instanceof Error ? err.message : String(err)}`,
        duration_ms: Date.now() - start,
      };
    }
  },
};
