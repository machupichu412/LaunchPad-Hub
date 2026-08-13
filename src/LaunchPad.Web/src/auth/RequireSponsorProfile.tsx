import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Spinner } from '@fluentui/react-components';
import { getMySponsorProfile } from '../api/sponsors';

/**
 * Wraps Sponsor-only routes that assume a Sponsor DB row exists (My Projects, My
 * Candidates, matches, reviews). RequireRole only checks the Entra role — it says
 * nothing about whether AppUserProvisioningMiddleware's JIT AppUser row has a
 * matching Sponsor row yet (see SponsorOnboarding.tsx, the create flow that fills
 * this gap). Redirects to /sponsor-onboarding when it doesn't. Mirrors
 * RequireCandidateProfile.
 *
 * Uses the same ['sponsors','me'] query key as SponsorOnboarding.tsx/NavMenu.tsx —
 * react-query dedupes/caches it, so nesting this on every Sponsor route adds no
 * extra network round-trips beyond the first.
 */
export function RequireSponsorProfile({ children }: { children: ReactNode }) {
  const { data: profile, isLoading } = useQuery({
    queryKey: ['sponsors', 'me'],
    queryFn: getMySponsorProfile,
  });

  if (isLoading) {
    return <Spinner label="Loading your profile..." />;
  }

  if (!profile) {
    return <Navigate to="/sponsor-onboarding" replace />;
  }

  return <>{children}</>;
}
