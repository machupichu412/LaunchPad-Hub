import { authedFetch } from './authedFetch';
import type { CandidateDashboardDto, CandidateDto, UpdateCandidateProfileRequest } from './types';

export async function getCandidate(candidateId: number): Promise<CandidateDto> {
  const response = await authedFetch(`/api/candidates/${candidateId}`);
  if (!response.ok) throw new Error(`Failed to load candidate ${candidateId}: ${response.status}`);
  return response.json() as Promise<CandidateDto>;
}

export async function getCandidatesByCohort(cohortId: number): Promise<CandidateDto[]> {
  const response = await authedFetch(`/api/candidates/cohort/${cohortId}`);
  if (!response.ok) throw new Error(`Failed to load candidates for cohort ${cohortId}: ${response.status}`);
  return response.json() as Promise<CandidateDto[]>;
}

export async function getMyCandidateProfile(): Promise<CandidateDto | null> {
  const response = await authedFetch('/api/candidates/me');
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to load your profile: ${response.status}`);
  return response.json() as Promise<CandidateDto>;
}

export async function updateMyCandidateProfile(request: UpdateCandidateProfileRequest): Promise<CandidateDto> {
  const response = await authedFetch('/api/candidates/me', { method: 'PUT', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to update your profile: ${response.status}`);
  return response.json() as Promise<CandidateDto>;
}

export async function getMyCandidateDashboard(): Promise<CandidateDashboardDto> {
  const response = await authedFetch('/api/candidates/me/dashboard');
  if (!response.ok) throw new Error(`Failed to load your dashboard: ${response.status}`);
  return response.json() as Promise<CandidateDashboardDto>;
}
