import type { StateDefinition } from "../../types.js";
import { pass, fail } from "../../types.js";

export const buildingState: StateDefinition = {
  emoji: "[BLD]",
  stageType: "gate",
  skill: null,
  canTransitionTo: ["LINTING", "BLOCKED"],
  allowedOperations: ["run-gate"],

  transitionGuard: (ctx) => {
    const gate = ctx.state.gateResults["BUILDING"];
    if (!gate?.passed) return fail("Build gate has not passed. Run run-gate BUILDING first.");
    return pass();
  },
};
