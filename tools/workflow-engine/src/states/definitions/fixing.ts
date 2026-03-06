import type { StateDefinition, WorkflowState } from "../../types.js";
import { pass, fail } from "../../types.js";

export const fixingState: StateDefinition = {
  emoji: "[FIX]",
  stageType: "agent",
  skill: null,
  canTransitionTo: ["BUILDING", "BLOCKED"],
  allowedOperations: ["signal-done"],

  transitionGuard: (ctx) => {
    if (!ctx.state.developerDone) return fail("Developer has not signalled done. Run signal-done first.");
    if (ctx.state.fixAttempts >= 3) return fail("Maximum fix attempts (3) reached. Transition to BLOCKED.");
    return pass();
  },

  onEntry: (state): WorkflowState => ({
    ...state,
    developerDone: false,
    reviewApproved: null,
    reviewReport: "",
    verifyPassed: null,
    verifyReport: "",
    gateResults: {},
    fixAttempts: state.fixAttempts + 1,
  }),
};
