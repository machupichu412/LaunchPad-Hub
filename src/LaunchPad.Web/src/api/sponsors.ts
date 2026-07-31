import { authedFetch } from './authedFetch';
import type { SponsorDto } from './types';

export async function getMySponsorProfile(): Promise<SponsorDto | null> {
  const response = await authedFetch('/api/sponsors/me');
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to load sponsor profile: ${response.status}`);
  return response.json() as Promise<SponsorDto>;
}
