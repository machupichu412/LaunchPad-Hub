import { authedFetch } from './authedFetch';
import type { CreateSponsorProfileRequest, SponsorCandidateDto, SponsorDto } from './types';

export async function getMySponsorProfile(): Promise<SponsorDto | null> {
  const response = await authedFetch('/api/sponsors/me');
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Failed to load sponsor profile: ${response.status}`);
  return response.json() as Promise<SponsorDto>;
}

export async function createMySponsorProfile(request: CreateSponsorProfileRequest): Promise<SponsorDto> {
  const response = await authedFetch('/api/sponsors/me', { method: 'POST', body: JSON.stringify(request) });
  if (!response.ok) {
    // Conflict bodies here are a plain JSON-encoded string (e.g. "profile already
    // exists") worth surfacing verbatim rather than collapsing to a generic message.
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
  return response.json() as Promise<SponsorDto>;
}

export async function getMyCandidates(): Promise<SponsorCandidateDto[]> {
  const response = await authedFetch('/api/sponsors/me/candidates');
  if (!response.ok) throw new Error(`Failed to load your candidates: ${response.status}`);
  return response.json() as Promise<SponsorCandidateDto[]>;
}
