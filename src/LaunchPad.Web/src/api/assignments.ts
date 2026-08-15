import { authedFetch } from './authedFetch';
import type {
  CandidateEvaluationDto,
  CreateTodoRequest,
  DeliverableDto,
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

export async function createTodo(assignmentId: number, request: CreateTodoRequest): Promise<ProjectTodoDto> {
  const response = await authedFetch(`/api/assignments/${assignmentId}/todos`, {
    method: 'POST',
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new Error(`Failed to create task: ${response.status}`);
  return response.json() as Promise<ProjectTodoDto>;
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

export async function submitDeliverable(
  assignmentId: number,
  params: { title: string; projectTodoId: number | null; file: File },
): Promise<DeliverableDto> {
  const formData = new FormData();
  formData.set('title', params.title);
  if (params.projectTodoId !== null) formData.set('projectTodoId', String(params.projectTodoId));
  formData.set('file', params.file);

  // No explicit Content-Type here — authedFetch skips its JSON default for a FormData
  // body so the browser can set its own multipart boundary.
  const response = await authedFetch(`/api/assignments/${assignmentId}/deliverables`, {
    method: 'POST',
    body: formData,
  });
  if (!response.ok) throw new Error(`Failed to submit deliverable: ${response.status}`);
  return response.json() as Promise<DeliverableDto>;
}

/** Fetches a deliverable's file content as an authenticated blob — mirrors getCandidateAvatarBlob's shape. */
export async function downloadDeliverableFile(assignmentId: number, deliverableId: number): Promise<Blob> {
  const response = await authedFetch(`/api/assignments/${assignmentId}/deliverables/${deliverableId}/file`);
  if (!response.ok) throw new Error(`Failed to download deliverable: ${response.status}`);
  return response.blob();
}

export async function getAssignmentEvaluations(assignmentId: number): Promise<CandidateEvaluationDto[]> {
  const response = await authedFetch(`/api/assignments/${assignmentId}/evaluations`);
  if (!response.ok) throw new Error(`Failed to load evaluations: ${response.status}`);
  return response.json() as Promise<CandidateEvaluationDto[]>;
}
