import Anthropic from "@anthropic-ai/sdk";
import type { StepDefinition, WorkflowContext, StepResult } from "../../types.js";
import { log } from "../../util/logger.js";

export const fix: StepDefinition = {
  id: "fix",
  name: "Auto-fix Issues",
  kind: "agent",
  canRetry: false,
  async run(ctx: WorkflowContext): Promise<StepResult> {
    const start = Date.now();

    // Gather issues from review and verify results
    const issues: string[] = [];

    const reviewResult = ctx.stepResults["review"];
    if (reviewResult?.status === "failed" && reviewResult.output) {
      issues.push(`## Code Review Issues\n${reviewResult.output}`);
    }

    const verifyResult = ctx.stepResults["verify"];
    if (verifyResult?.status === "failed" && verifyResult.output) {
      issues.push(`## QA Verification Issues\n${verifyResult.output}`);
    }

    if (issues.length === 0) {
      return {
        status: "passed",
        output: "No issues to fix",
        duration_ms: Date.now() - start,
      };
    }

    const client = new Anthropic();

    log.info(`Auto-fix attempt ${ctx.retryCount + 1}...`);

    try {
      const response = await client.messages.create({
        model: "claude-sonnet-4-20250514",
        max_tokens: 16384,
        system: "You are a code fix agent. Analyze the issues reported and describe the fixes needed.",
        messages: [
          {
            role: "user",
            content: [
              `Fix the following issues found during review/verification on branch \`${ctx.branch}\`:\n`,
              ...issues,
              "\nDescribe the specific fixes needed for each issue.",
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
        artifacts: { fixes: text },
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
