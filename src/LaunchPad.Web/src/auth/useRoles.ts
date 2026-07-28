import { useMsal } from '@azure/msal-react';
import type { AppRole } from './roles';

export function useRoles(): AppRole[] {
  const { accounts } = useMsal();
  return (accounts[0]?.idTokenClaims?.roles as AppRole[] | undefined) ?? [];
}
