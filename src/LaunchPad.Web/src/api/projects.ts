import { authedFetch } from './authedFetch';
import type { CreateProjectRequest, ProjectDto, RateInterestRequest, RejectProjectRequest, UpdateProjectRequest } from './types';

export async function getMyProjects(): Promise<ProjectDto[]> {
  const response = await authedFetch('/api/projects/mine');
  if (!response.ok) throw new Error(`Failed to load your projects: ${response.status}`);
  return response.json() as Promise<ProjectDto[]>;
}

export async function getOpenProjects(): Promise<ProjectDto[]> {
  const response = await authedFetch('/api/projects/open');
  if (!response.ok) throw new Error(`Failed to load open projects: ${response.status}`);
  return response.json() as Promise<ProjectDto[]>;
}

export async function getProject(projectId: number): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}`);
  if (!response.ok) throw new Error(`Failed to load project ${projectId}: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}

export async function getProjectsByCohort(cohortId: number): Promise<ProjectDto[]> {
  const response = await authedFetch(`/api/projects/cohort/${cohortId}`);
  if (!response.ok) throw new Error(`Failed to load projects for cohort ${cohortId}: ${response.status}`);
  return response.json() as Promise<ProjectDto[]>;
}

export async function createProject(request: CreateProjectRequest): Promise<ProjectDto> {
  const response = await authedFetch('/api/projects', { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to create project: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}

export async function updateProject(projectId: number, request: UpdateProjectRequest): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}`, { method: 'PUT', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to update project ${projectId}: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}

export async function getOpenProjectDetail(projectId: number): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}/open-detail`);
  if (!response.ok) throw new Error(`Failed to load project ${projectId}: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}

export async function rateProjectInterest(projectId: number, request: RateInterestRequest): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}/interest`, { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to rate project ${projectId}: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}

export async function submitProject(projectId: number): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}/submit`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to submit project ${projectId}: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}

export async function getPendingApprovalProjects(cohortId: number): Promise<ProjectDto[]> {
  const response = await authedFetch(`/api/projects/pending-approval?cohortId=${cohortId}`);
  if (!response.ok) throw new Error(`Failed to load pending-approval projects: ${response.status}`);
  return response.json() as Promise<ProjectDto[]>;
}

export async function approveProject(projectId: number): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}/approve`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to approve project ${projectId}: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}

export async function rejectProject(projectId: number, request: RejectProjectRequest): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}/reject`, { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to reject project ${projectId}: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}
