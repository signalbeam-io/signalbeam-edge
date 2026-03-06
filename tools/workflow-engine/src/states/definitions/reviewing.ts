import type { StateDefinition } from "../../types.js";
import { pass, fail } from "../../types.js";

export const reviewingState: StateDefinition = {
  emoji: "[REV]",
  stageType: "agent",
  skill: "code-review",
  canTransitionTo: ["CREATING_PR", "FIXING", "BLOCKED"],
  allowedOperations: ["review-done"],

  transitionGuard: (ctx) => {
    if (ctx.state.reviewApproved === null) return fail("Review not completed. Reviewer must signal review-done first.");
    return pass();
  },
};
