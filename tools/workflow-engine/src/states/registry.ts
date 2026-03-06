import type { StateName, StateDefinition } from "../types.js";
import { preflightState } from "./definitions/preflight.js";
import { buildingState } from "./definitions/building.js";
import { lintingState } from "./definitions/linting.js";
import { testingState } from "./definitions/testing.js";
import { reviewingState } from "./definitions/reviewing.js";
import { verifyingState } from "./definitions/verifying.js";
import { fixingState } from "./definitions/fixing.js";
import { creatingPrState } from "./definitions/creating-pr.js";
import { blockedState } from "./definitions/blocked.js";
import { completeState } from "./definitions/complete.js";

export const STATE_REGISTRY: Record<StateName, StateDefinition> = {
  PREFLIGHT: preflightState,
  BUILDING: buildingState,
  LINTING: lintingState,
  TESTING: testingState,
  REVIEWING: reviewingState,
  VERIFYING: verifyingState,
  FIXING: fixingState,
  CREATING_PR: creatingPrState,
  BLOCKED: blockedState,
  COMPLETE: completeState,
};

export const VALID_STATES = Object.keys(STATE_REGISTRY) as StateName[];

export function isValidState(s: string): s is StateName {
  return VALID_STATES.includes(s as StateName);
}

export function getStateDefinition(s: StateName): StateDefinition {
  return STATE_REGISTRY[s];
}
