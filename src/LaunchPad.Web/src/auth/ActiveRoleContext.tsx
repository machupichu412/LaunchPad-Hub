import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { AppRoles, type AppRole } from './roles';
import { useRoles } from './useRoles';

type ActiveRoleState = {
  activeRole: AppRole | null;
  roles: AppRole[];
  isLoading: boolean;
  setActiveRole: (role: AppRole) => void;
};

const ActiveRoleContext = createContext<ActiveRoleState | undefined>(undefined);

/**
 * The header's role-switcher pills imply a single "currently viewing as"
 * perspective, not "show everything every held role can see" — that's what
 * NavMenu used before this context existed. Defaults to Candidate when held
 * (this build-out's primary persona), else the first role on the token.
 *
 * Purely a UI concern: RequireRole still gates routes on ANY held role, so
 * switching the active role never grants or removes access — see CLAUDE.md's
 * "client-side route guards are navigation UX only."
 */
export function ActiveRoleProvider({ children }: { children: ReactNode }) {
  const { roles, isLoading } = useRoles();
  const [activeRole, setActiveRoleState] = useState<AppRole | null>(null);

  useEffect(() => {
    if (isLoading) return;
    setActiveRoleState((current) => {
      if (current && roles.includes(current)) return current;
      if (roles.includes(AppRoles.Candidate)) return AppRoles.Candidate;
      return roles[0] ?? null;
    });
  }, [roles, isLoading]);

  // A fresh object here re-renders every consumer on every provider render, and this
  // provider wraps the entire router. setActiveRoleState is a useState setter, so it is
  // already stable and does not belong in the dependency list.
  const value = useMemo(
    () => ({ activeRole, roles, isLoading, setActiveRole: setActiveRoleState }),
    [activeRole, roles, isLoading],
  );

  return <ActiveRoleContext.Provider value={value}>{children}</ActiveRoleContext.Provider>;
}

export function useActiveRole(): ActiveRoleState {
  const ctx = useContext(ActiveRoleContext);
  if (!ctx) throw new Error('useActiveRole must be used within an ActiveRoleProvider');
  return ctx;
}
