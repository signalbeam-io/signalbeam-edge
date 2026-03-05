import Anthropic from "@anthropic-ai/sdk";
import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { loadSkill } from "../../skills/loader.js";
import { buildSystemPrompt } from "../../skills/mapper.js";
import { shell } from "../../util/shell.js";
import { log } from "../../util/logger.js";

export const implement: StepDefinition = {
  id: "implement",
  name: "Implementation",
  kind: "agent",
  skill: "start-work",
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const skill = loadSkill(ctx.repoRoot, "start-work");

    if (!skill) {
      return {
        status: "failed",
        output: "Could not load start-work skill",
        duration_ms: Date.now() - start,
      };
    }

    if (ctx.issueNumber == null) {
      return {
        status: "failed",
        output: "No issue number available",
        duration_ms: Date.now() - start,
      };
    }

    const { stdout: issueJson, exitCode } = shell(
      `gh issue view ${ctx.issueNumber} --json number,title,body,labels`,
      { cwd: ctx.repoRoot },
    );

    if (exitCode !== 0) {
      return {
        status: "failed",
        output: `Could not fetch issue #${ctx.issueNumber}`,
        duration_ms: Date.now() - start,
      };
    }

    const systemPrompt = buildSystemPrompt(ctx.repoRoot, skill, ctx.changedFiles);
    const client = new Anthropic();

    log.info("Starting implementation agent (this may take a while)...");

    try {
      const response = await client.messages.create({
        model: "claude-sonnet-4-20250514",
        max_tokens: 16384,
        system: systemPrompt,
        messages: [
          {
            role: "user",
            content: [
              `Implement the following issue on branch \`${ctx.branch}\`.\n`,
              `## Issue\n\`\`\`json\n${issueJson}\n\`\`\`\n`,
              "Analyze the requirements and describe the implementation plan. ",
              "List the files that need to be created or modified, in layer order: ",
              "Domain -> Application -> Infrastructure -> Endpoints -> Frontend -> Tests.",
            ].join("\n"),
          },
        ],
      });

      const text = response.content
        .filter((b): b is Anthropic.TextBlock => b.type === "text")
        .map((b) => b.text)
        .join("\n");

      return {
        status: "passed",
        output: text,
        artifacts: { plan: text },
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
