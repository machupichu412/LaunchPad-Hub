import { useMsal } from '@azure/msal-react';
import { useApiAccessTokenClaims } from './useApiAccessTokenClaims';

/** Diagnostic-only: shows the SPA ID token and API access token side by side. */
export function useApiTokenDiagnostics() {
  const { accounts } = useMsal();
  const { claims: apiAccessTokenClaims, isLoading } = useApiAccessTokenClaims();

  return {
    spaIdTokenClaims: accounts[0]?.idTokenClaims ?? null,
    apiAccessTokenClaims,
    isLoading,
    roles: (apiAccessTokenClaims?.roles as string[] | undefined) ?? [],
  };
}
