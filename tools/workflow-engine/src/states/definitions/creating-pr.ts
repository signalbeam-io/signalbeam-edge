import type { StateDefinition } from "../../types.js";
import { pass, fail } from "../../types.js";

export const creatingPrState: StateDefinition = {
  emoji: "[PR]",
  stageType: "agent",
  skill: "create-pr",
  canTransitionTo: ["COMPLETE", "BLOCKED"],
  allowedOperations: ["record-pr"],

  transitionGuard: (ctx) => {
    if (!ctx.state.reviewApproved) return fail("Code review not approved.");
    if (!ctx.state.verifyPassed) return fail("QA verification not passed.");
    if (!ctx.state.prNumber) return fail("No PR recorded. Run record-pr first.");
    return pass();
  },
};
