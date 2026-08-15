import { authedFetch } from './authedFetch';
import type { CohortDto, CohortStatus, CreateCohortRequest } from './types';

export async function getCohorts(): Promise<CohortDto[]> {
  const response = await authedFetch('/api/cohorts');
  if (!response.ok) throw new Error(`Failed to load cohorts: ${response.status}`);
  return response.json() as Promise<CohortDto[]>;
}

export async function createCohort(request: CreateCohortRequest): Promise<CohortDto> {
  const response = await authedFetch('/api/cohorts', { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) throw new Error(`Failed to create cohort: ${response.status}`);
  return response.json() as Promise<CohortDto>;
}

export async function updateCohortStatus(cohortId: number, status: CohortStatus): Promise<CohortDto> {
  const response = await authedFetch(`/api/cohorts/${cohortId}/status`, { method: 'PATCH', body: JSON.stringify({ status }) });
  if (!response.ok) throw new Error(`Failed to update cohort status: ${response.status}`);
  return response.json() as Promise<CohortDto>;
}
