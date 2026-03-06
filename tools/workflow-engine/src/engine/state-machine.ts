import type {
  StateName,
  WorkflowState,
  GuardContext,
  PreconditionResult,
  ProjectConfig,
  GitInfo,
} from "../types.js";
import { pass, fail } from "../types.js";
import { getStateDefinition, isValidState } from "../states/registry.js";
import { appendEvent } from "../persistence/state-store.js";

export function transition(
  state: WorkflowState,
  target: string,
  config: ProjectConfig,
  gitInfo: GitInfo,
): { result: PreconditionResult; newState: WorkflowState } {
  if (!isValidState(target)) {
    return { result: fail(`Invalid state: ${target}`), newState: state };
  }

  const currentDef = getStateDefinition(state.state);
  const targetName = target as StateName;

  // Check if transition is allowed
  if (!currentDef.canTransitionTo.includes(targetName)) {
    return {
      result: fail(
        `Cannot transition from ${state.state} to ${targetName}. Allowed: ${currentDef.canTransitionTo.join(", ")}`,
      ),
      newState: state,
    };
  }

  // Run current state's transition guard
  const ctx: GuardContext = {
    state,
    config,
    gitInfo,
    from: state.state,
  };

  const guardResult = currentDef.transitionGuard(ctx);
  if (!guardResult.pass) {
    return { result: guardResult, newState: state };
  }

  // Apply transition
  let newState: WorkflowState = {
    ...state,
    state: targetName,
  };

  // Run target state's onEntry hook
  const targetDef = getStateDefinition(targetName);
  if (targetDef.onEntry) {
    newState = targetDef.onEntry(newState, ctx);
  }

  // Log the transition
  newState = appendEvent(newState, "transition", {
    from: state.state,
    to: targetName,
  });

  return { result: pass(), newState };
}

export function isOperationAllowed(state: WorkflowState, operation: string): PreconditionResult {
  const def = getStateDefinition(state.state);
  if (!def.allowedOperations.includes(operation)) {
    return fail(
      `Operation '${operation}' not allowed in state ${state.state}. Allowed: ${def.allowedOperations.join(", ") || "none"}`,
    );
  }
  return pass();
}

export function getStateSkill(state: WorkflowState): string | null {
  return getStateDefinition(state.state).skill;
}

export function getStateEmoji(state: WorkflowState): string {
  return getStateDefinition(state.state).emoji;
}

export function getStageType(state: WorkflowState): import("../types.js").StageType {
  return getStateDefinition(state.state).stageType;
}
