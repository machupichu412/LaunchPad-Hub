import { useEffect, useState } from 'react';
import { useMsal } from '@azure/msal-react';
import { apiRequest } from './msalConfig';

export type ApiTokenClaims = {
  roles?: string[];
  groups?: string[];
  aud?: string;
  [key: string]: unknown;
};

function decodeJwtPayload(token: string): ApiTokenClaims | null {
  try {
    const payload = token.split('.')[1];
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = normalized.padEnd(normalized.length + ((4 - (normalized.length % 4)) % 4), '=');

    return JSON.parse(atob(padded)) as ApiTokenClaims;
  } catch {
    return null;
  }
}

export type ApiAccessTokenState = {
  claims: ApiTokenClaims | null;
  /**
   * True until the first acquireTokenSilent call settles. Callers that make an
   * access-control decision (RequireRole) MUST wait for this to go false before
   * treating an empty roles list as "confirmed no access" — acquireTokenSilent
   * is async, so claims start out null on every mount regardless of the user's
   * actual roles, and a redirect fired on that transient empty state fires
   * before the real claims ever arrive.
   */
  isLoading: boolean;
};

/**
 * The API access token (audience = LaunchPad-API, not the SPA) is the
 * authoritative source for roles — it's the exact token the API itself
 * validates, so client-side UI shaping reads the same claims the server
 * enforces rather than a separately-maintained copy on the SPA registration.
 * The SPA's ID token does NOT carry a reliable roles claim for group-based
 * assignments at scale — see the group-membership overage note in CLAUDE.md.
 */
export function useApiAccessTokenClaims(): ApiAccessTokenState {
  const { instance, accounts } = useMsal();
  const [claims, setClaims] = useState<ApiTokenClaims | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const account = accounts[0];
    if (!account) {
      setClaims(null);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    instance
      .acquireTokenSilent({ ...apiRequest, account })
      .then((response) => setClaims(decodeJwtPayload(response.accessToken)))
      .catch((error) => {
        console.error('API token acquisition failed', error);
        setClaims(null);
      })
      .finally(() => setIsLoading(false));
  }, [accounts, instance]);

  return { claims, isLoading };
}
