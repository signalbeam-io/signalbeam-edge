import type { StateDefinition } from "../../types.js";
import { pass, fail } from "../../types.js";

export const verifyingState: StateDefinition = {
  emoji: "[QA]",
  stageType: "agent",
  skill: "task-check",
  canTransitionTo: ["CREATING_PR", "FIXING", "BLOCKED"],
  allowedOperations: ["verify-done"],

  transitionGuard: (ctx) => {
    if (ctx.state.verifyPassed === null) return fail("Verification not completed. Verifier must signal verify-done first.");
    return pass();
  },
};
