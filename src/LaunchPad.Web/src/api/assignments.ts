import { authedFetch } from './authedFetch';
import type {
  CandidateEvaluationDto,
  DeliverableDto,
  CreateDeliverableRequest,
  MyAssignmentDto,
  ProjectTodoDto,
  UpdateTodoStatusRequest,
} from './types';

export async function getMyAssignment(): Promise<MyAssignmentDto | null> {
  const response = await authedFetch('/api/assignments/mine');
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to load your assignment: ${response.status}`);
  return response.json() as Promise<MyAssignmentDto>;
}

export async function getAssignmentTodos(assignmentId: number): Promise<ProjectTodoDto[]> {
  const response = await authedFetch(`/api/assignments/${assignmentId}/todos`);
  if (!response.ok) throw new Error(`Failed to load tasks: ${response.status}`);
  return response.json() as Promise<ProjectTodoDto[]>;
}

export async function updateTodoStatus(
  assignmentId: number,
  todoId: number,
  request: UpdateTodoStatusRequest,
): Promise<ProjectTodoDto> {
  const response = await authedFetch(`/api/assignments/${assignmentId}/todos/${todoId}`, {
    method: 'PATCH',
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new Error(`Failed to update task: ${response.status}`);
  return response.json() as Promise<ProjectTodoDto>;
}

export async function getAssignmentDeliverables(assignmentId: number): Promise<DeliverableDto[]> {
  const response = await authedFetch(`/api/assignments/${assignmentId}/deliverables`);
  if (!response.ok) throw new Error(`Failed to load deliverables: ${response.status}`);
  return response.json() as Promise<DeliverableDto[]>;
}

export async function createDeliverable(
  assignmentId: number,
  request: CreateDeliverableRequest,
): Promise<DeliverableDto> {
  const response = await authedFetch(`/api/assignments/${assignmentId}/deliverables`, {
    method: 'POST',
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new Error(`Failed to submit deliverable: ${response.status}`);
  return response.json() as Promise<DeliverableDto>;
}

export async function getAssignmentEvaluations(assignmentId: number): Promise<CandidateEvaluationDto[]> {
  const response = await authedFetch(`/api/assignments/${assignmentId}/evaluations`);
  if (!response.ok) throw new Error(`Failed to load evaluations: ${response.status}`);
  return response.json() as Promise<CandidateEvaluationDto[]>;
}
