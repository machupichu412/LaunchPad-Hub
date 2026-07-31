import { authedFetch } from './authedFetch';
import type { OpsDashboardDto, RiskCandidateDto } from './types';

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
