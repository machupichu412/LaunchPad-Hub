import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { Spinner } from '@fluentui/react-components';
import type { AppRole } from './roles';
import { useRoles } from './useRoles';

/**
 * Navigation UX only — it shapes the menu. The API independently re-checks every
 * request, so this guard is not a security boundary. See CLAUDE.md.
 *
 * Roles come from an async API access token acquisition (see useRoles), so we
 * must wait for isLoading to clear before deciding to redirect — otherwise every
 * role-gated route bounces to /unauthorized on the transient empty state that
 * exists before the token round-trip completes, regardless of the user's actual
 * roles.
 */
export function RequireRole({ allow, children }: { allow: AppRole[]; children: ReactNode }) {
  const { roles, isLoading } = useRoles();

  if (isLoading) {
    return <Spinner label="Checking access..." />;
  }

  if (!roles.some((r) => allow.includes(r))) {
    return <Navigate to="/unauthorized" replace />;
  }

  return <>{children}</>;
}
