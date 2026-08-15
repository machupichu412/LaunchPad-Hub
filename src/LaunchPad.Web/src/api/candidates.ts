import { authedFetch } from './authedFetch';
import type {
  CandidateDashboardDto,
  CandidateDto,
  CreateCandidateProfileRequest,
  UpdateCandidateProfileRequest,
  UpdateCandidateStatusRequest,
} from './types';

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

export async function createMyCandidateProfile(request: CreateCandidateProfileRequest): Promise<CandidateDto> {
  const response = await authedFetch('/api/candidates/me', { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) {
    // Conflict/BadRequest bodies here are a plain JSON-encoded string (e.g. "no
    // active cohort" vs. "profile already exists") worth surfacing verbatim rather
    // than collapsing to a generic status-code message.
    const raw = await response.text().catch(() => '');
    let message = raw;
    try {
      const parsed = JSON.parse(raw);
      if (typeof parsed === 'string') message = parsed;
    } catch {
      // not JSON — use the raw text as-is
    }
    throw new Error(message || `Failed to create your profile: ${response.status}`);
  }
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

export async function updateCandidateStatus(candidateId: number, request: UpdateCandidateStatusRequest): Promise<CandidateDto> {
  const response = await authedFetch(`/api/candidates/${candidateId}/status`, {
    method: 'PATCH',
    body: JSON.stringify(request),
  });
  if (!response.ok) throw new Error(`Failed to update candidate status: ${response.status}`);
  return response.json() as Promise<CandidateDto>;
}

/** null when the candidate has no photo set (server returns 404) — not an error. */
export async function getCandidateAvatarBlob(candidateId: number): Promise<Blob | null> {
  const response = await authedFetch(`/api/candidates/${candidateId}/avatar`);
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to load photo for candidate ${candidateId}: ${response.status}`);
  return response.blob();
}
