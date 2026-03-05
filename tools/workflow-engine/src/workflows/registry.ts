import type { WorkflowDefinition } from "../types.js";
import { standardWorkflow } from "./standard.js";
import { infraWorkflow } from "./infra.js";
import { startWorkWorkflow } from "./start-work.js";

const workflows = new Map<string, WorkflowDefinition>([
  [standardWorkflow.id, standardWorkflow],
  [infraWorkflow.id, infraWorkflow],
  [startWorkWorkflow.id, startWorkWorkflow],
]);

export function getWorkflow(id: string): WorkflowDefinition | undefined {
  return workflows.get(id);
}

export function listWorkflows(): string[] {
  return [...workflows.keys()];
}
