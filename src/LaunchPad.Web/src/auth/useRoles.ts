import { useApiAccessTokenClaims } from './useApiAccessTokenClaims';
import type { AppRole } from './roles';

export type RolesState = {
  roles: AppRole[];
  isLoading: boolean;
};

// Module-level so the no-roles case returns the same array identity every call. A fresh
// [] was enough to invalidate every memo and effect keyed on roles, on every render.
const NO_ROLES: AppRole[] = [];

/**
 * Reads roles from the API access token, not the SPA's ID token — see
 * useApiAccessTokenClaims for why. This is navigation UX only; the API
 * independently re-checks every request, so this is never a security boundary.
 *
 * isLoading MUST be checked before treating an empty roles array as "no
 * access" — see useApiAccessTokenClaims for the race this guards against.
 */
export function useRoles(): RolesState {
  const { claims, isLoading } = useApiAccessTokenClaims();
  return { roles: (claims?.roles as AppRole[] | undefined) ?? NO_ROLES, isLoading };
}
