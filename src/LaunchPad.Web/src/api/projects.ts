import { authedFetch } from './authedFetch';
import type {
  CreateProjectRequest,
  ProjectDto,
  ProjectMatchDto,
  RateInterestRequest,
  RejectProjectRequest,
  SponsorCandidateDto,
  SponsorCandidateMatchDto,
  UpdateDeliveryStageRequest,
  UpdateProjectRequest,
} from './types';

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

export async function getEligibleCandidates(projectId: number): Promise<SponsorCandidateMatchDto[]> {
  const response = await authedFetch(`/api/projects/${projectId}/eligible-candidates`);
  if (!response.ok) throw new Error(`Failed to load eligible candidates: ${response.status}`);
  return response.json() as Promise<SponsorCandidateMatchDto[]>;
}

export async function requestAssignment(projectId: number, candidateId: number): Promise<ProjectMatchDto> {
  const response = await authedFetch(`/api/projects/${projectId}/candidates/${candidateId}/request`, { method: 'POST' });
  if (!response.ok) throw new Error(`Failed to request candidate: ${response.status}`);
  return response.json() as Promise<ProjectMatchDto>;
}

export async function getAssignedCandidates(projectId: number): Promise<SponsorCandidateDto[]> {
  const response = await authedFetch(`/api/projects/${projectId}/assigned-candidates`);
  if (!response.ok) throw new Error(`Failed to load assigned candidates: ${response.status}`);
  return response.json() as Promise<SponsorCandidateDto[]>;
}

export async function cancelProject(projectId: number, request: RejectProjectRequest): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}/cancel`, { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to cancel project ${projectId}: ${response.status}`);
  return response.json() as Promise<ProjectDto>;
}

/** Advances (Sponsor, forward-only) or corrects (Program Ops, any value) this project's
 * delivery-stage milestone — backs the Executive KPI dashboard. A 400 here means a Sponsor
 * tried to move backward; surface response text as the error, not a generic status code. */
export async function updateDeliveryStage(projectId: number, request: UpdateDeliveryStageRequest): Promise<ProjectDto> {
  const response = await authedFetch(`/api/projects/${projectId}/delivery-stage`, { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) {
    const text = await response.text().catch(() => '');
    throw new Error(text || `Failed to update delivery stage for project ${projectId}: ${response.status}`);
  }
  return response.json() as Promise<ProjectDto>;
}
