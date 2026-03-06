import type { StateDefinition } from "../../types.js";
import { pass, fail } from "../../types.js";

export const preflightState: StateDefinition = {
  emoji: "[PRE]",
  stageType: "gate",
  skill: "check-architecture",
  canTransitionTo: ["BUILDING", "BLOCKED"],
  allowedOperations: ["record-issue", "record-branch", "run-gate"],

  transitionGuard: (ctx) => {
    if (!ctx.state.issueNumber) return fail("No issue number recorded. Run record-issue first.");
    if (!ctx.state.branch) return fail("No branch recorded. Run record-branch first.");
    if (!ctx.gitInfo.workingTreeClean) return fail("Working tree is dirty.");
    const gate = ctx.state.gateResults["PREFLIGHT"];
    if (!gate?.passed) return fail("Preflight gate has not passed. Run run-gate PREFLIGHT first.");
    return pass();
  },
};
