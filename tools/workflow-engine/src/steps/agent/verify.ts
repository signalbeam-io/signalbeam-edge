import Anthropic from "@anthropic-ai/sdk";
import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { loadSkill } from "../../skills/loader.js";
import { buildSystemPrompt } from "../../skills/mapper.js";
import { shell } from "../../util/shell.js";
import * as git from "../../util/git.js";
import { log } from "../../util/logger.js";

export const verify: StepDefinition = {
  id: "verify",
  name: "QA Verification",
  kind: "agent",
  skill: "task-check",
  tools: ["Read", "Glob", "Grep"],
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();
    const skill = loadSkill(ctx.repoRoot, "task-check");

    if (!skill) {
      return {
        status: "failed",
        output: "Could not load task-check skill",
        duration_ms: Date.now() - start,
      };
    }

    if (ctx.issueNumber == null) {
      return {
        status: "failed",
        output: "No issue number available for QA verification",
        duration_ms: Date.now() - start,
      };
    }

    // Fetch issue details
    const { stdout: issueJson, exitCode } = shell(
      `gh issue view ${ctx.issueNumber} --json number,title,body,labels,state`,
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
    const diffOutput = git.diff(ctx.repoRoot);
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
              `Verify the implementation on branch \`${ctx.branch}\` against issue #${ctx.issueNumber}.\n`,
              `## Issue\n\`\`\`json\n${issueJson}\n\`\`\`\n`,
              `## Changed Files\n\`\`\`\n${fileList}\n\`\`\`\n`,
              `## Diff\n\`\`\`diff\n${diffOutput.slice(0, 100_000)}\n\`\`\`\n`,
              "Return your verification. End with **Result: PASS** or **Result: FAIL**.",
            ].join("\n"),
          },
        ],
      });

      const text = response.content
        .filter((b): b is Anthropic.TextBlock => b.type === "text")
        .map((b) => b.text)
        .join("\n");

      const passed = /result:\s*pass/i.test(text);

      log.info(`QA verification: ${passed ? "PASS" : "FAIL"}`);

      return {
        status: passed ? "passed" : "failed",
        output: text,
        artifacts: { passed, verifyText: text },
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
