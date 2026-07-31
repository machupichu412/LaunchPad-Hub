import { authedFetch } from './authedFetch';
import type { PendingAssignmentDto, RunMatchingResult } from './types';

export async function runMatching(cohortId: number): Promise<RunMatchingResult> {
  const response = await authedFetch(`/api/matching/run?cohortId=${cohortId}`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to run matching: ${response.status}`);
  return response.json() as Promise<RunMatchingResult>;
}

export async function getMatchingQueue(cohortId: number): Promise<PendingAssignmentDto[]> {
  const response = await authedFetch(`/api/matching/queue?cohortId=${cohortId}`);
  if (!response.ok) throw new Error(`Failed to load the approval queue: ${response.status}`);
  return response.json() as Promise<PendingAssignmentDto[]>;
}

export async function approveMatch(assignmentId: number): Promise<PendingAssignmentDto> {
  const response = await authedFetch(`/api/matching/${assignmentId}/approve`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to approve match: ${response.status}`);
  return response.json() as Promise<PendingAssignmentDto>;
}

export async function denyMatch(assignmentId: number): Promise<PendingAssignmentDto> {
  const response = await authedFetch(`/api/matching/${assignmentId}/deny`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to deny match: ${response.status}`);
  return response.json() as Promise<PendingAssignmentDto>;
}
