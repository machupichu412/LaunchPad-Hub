import { authedFetch } from './authedFetch';
import type { ExecutiveDashboardDto, OpsDashboardDto, RiskCandidateDto } from './types';

export async function getOpsDashboard(): Promise<OpsDashboardDto> {
  const response = await authedFetch('/api/ops/dashboard');
  if (!response.ok) throw new Error(`Failed to load the dashboard: ${response.status}`);
  return response.json() as Promise<OpsDashboardDto>;
}

export async function getAtRiskCandidates(): Promise<RiskCandidateDto[]> {
  const response = await authedFetch('/api/ops/risks');
  if (!response.ok) throw new Error(`Failed to load risks: ${response.status}`);
  return response.json() as Promise<RiskCandidateDto[]>;
}

/** Single-cohort funnel + risk counts — see ExecutiveDashboardDto. */
export async function getExecutiveDashboard(cohortId: number): Promise<ExecutiveDashboardDto> {
  const response = await authedFetch(`/api/ops/executive-dashboard/${cohortId}`);
  if (!response.ok) throw new Error(`Failed to load the executive dashboard: ${response.status}`);
  return response.json() as Promise<ExecutiveDashboardDto>;
}
