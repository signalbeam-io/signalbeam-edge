import type { StateDefinition } from "../../types.js";
import { fail } from "../../types.js";

export const completeState: StateDefinition = {
  emoji: "[OK]",
  stageType: "terminal",
  skill: null,
  canTransitionTo: [],
  allowedOperations: [],

  transitionGuard: () => fail("COMPLETE is a terminal state. No transitions allowed."),
};
