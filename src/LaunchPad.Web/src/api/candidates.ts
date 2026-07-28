import { authedFetch } from './authedFetch';
import type { CandidateDto } from './types';

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
