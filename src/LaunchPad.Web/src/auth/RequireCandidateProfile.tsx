import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Spinner } from '@fluentui/react-components';
import { getMyCandidateProfile } from '../api/candidates';

/**
 * Wraps Candidate-only routes that assume a Candidate DB row exists (dashboard,
 * profile, assignments, marketplace, etc.). RequireRole only checks the Entra
 * role — it says nothing about whether AppUserProvisioningMiddleware's JIT
 * AppUser row has a matching Candidate row yet (see Onboarding.tsx, the create
 * flow that fills this gap). Redirects to /onboarding when it doesn't.
 *
 * Uses the same ['candidates','me'] query key as MyProfile.tsx/Onboarding.tsx —
 * react-query dedupes/caches it, so nesting this on every Candidate route adds no
 * extra network round-trips beyond the first.
 */
export function RequireCandidateProfile({ children }: { children: ReactNode }) {
  const { data: profile, isLoading } = useQuery({
    queryKey: ['candidates', 'me'],
    queryFn: getMyCandidateProfile,
  });

  if (isLoading) {
    return <Spinner label="Loading your profile..." />;
  }

  if (!profile) {
    return <Navigate to="/onboarding" replace />;
  }

  return <>{children}</>;
}
