import type { StateDefinition } from "../../types.js";
import { pass, fail } from "../../types.js";

export const testingState: StateDefinition = {
  emoji: "[TST]",
  stageType: "gate",
  skill: "run-tests",
  canTransitionTo: ["REVIEWING", "BLOCKED"],
  allowedOperations: ["run-gate"],

  transitionGuard: (ctx) => {
    const gate = ctx.state.gateResults["TESTING"];
    if (!gate?.passed) return fail("Test gate has not passed. Run run-gate TESTING first.");
    return pass();
  },
};
