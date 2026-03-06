import type { StateDefinition } from "../../types.js";
import { pass, fail } from "../../types.js";

export const lintingState: StateDefinition = {
  emoji: "[LNT]",
  stageType: "gate",
  skill: "lint",
  canTransitionTo: ["TESTING", "BLOCKED"],
  allowedOperations: ["run-gate"],

  transitionGuard: (ctx) => {
    const gate = ctx.state.gateResults["LINTING"];
    if (!gate?.passed) return fail("Lint gate has not passed. Run run-gate LINTING first.");
    return pass();
  },
};
