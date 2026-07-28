import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import type { AppRole } from './roles';
import { useRoles } from './useRoles';

/**
 * Navigation UX only — it shapes the menu. The API independently re-checks every
 * request, so this guard is not a security boundary. See CLAUDE.md.
 */
export function RequireRole({ allow, children }: { allow: AppRole[]; children: ReactNode }) {
  const roles = useRoles();
  if (!roles.some((r) => allow.includes(r))) {
    return <Navigate to="/unauthorized" replace />;
  }
  return <>{children}</>;
}
