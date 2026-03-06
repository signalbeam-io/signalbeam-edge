import type { StateDefinition, StateName } from "../../types.js";
import { pass } from "../../types.js";

const ALL_STATES: StateName[] = [
  "PREFLIGHT", "BUILDING", "LINTING", "TESTING",
  "REVIEWING", "VERIFYING", "FIXING", "CREATING_PR",
];

export const blockedState: StateDefinition = {
  emoji: "[!!!]",
  stageType: "terminal",
  skill: null,
  canTransitionTo: ALL_STATES,
  allowedOperations: [],

  transitionGuard: () => pass(),
};
