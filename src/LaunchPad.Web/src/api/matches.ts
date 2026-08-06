import { authedFetch } from './authedFetch';
import type { ProjectMatchDto } from './types';

export async function getProjectMatches(projectId: number): Promise<ProjectMatchDto[]> {
  const response = await authedFetch(`/api/projects/${projectId}/matches`);
  if (!response.ok) throw new Error(`Failed to load matches: ${response.status}`);
  return response.json() as Promise<ProjectMatchDto[]>;
}

export async function recommendMatch(projectId: number, assignmentId: number): Promise<ProjectMatchDto> {
  const response = await authedFetch(`/api/projects/${projectId}/matches/${assignmentId}/recommend`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to recommend match: ${response.status}`);
  return response.json() as Promise<ProjectMatchDto>;
}

export async function rejectMatch(projectId: number, assignmentId: number): Promise<ProjectMatchDto> {
  const response = await authedFetch(`/api/projects/${projectId}/matches/${assignmentId}/reject`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to reject match: ${response.status}`);
  return response.json() as Promise<ProjectMatchDto>;
}
