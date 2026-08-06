// Must match the appRoles values on the LaunchPad-API app registration manifest —
// see launchpad-build-guide.md §6.2 and LaunchPad.Application.Common.Roles server-side.
export const AppRoles = {
  Executive: 'LaunchPad.Executive',
  ProgramOps: 'LaunchPad.ProgramOps',
  Sponsor: 'LaunchPad.Sponsor',
  Candidate: 'LaunchPad.Candidate',
  HiringManager: 'LaunchPad.HiringManager',
} as const;

export type AppRole = (typeof AppRoles)[keyof typeof AppRoles];

const roleLabels: Record<AppRole, string> = {
  [AppRoles.Executive]: 'Executive',
  [AppRoles.ProgramOps]: 'Program Ops',
  [AppRoles.Sponsor]: 'Sponsor',
  [AppRoles.Candidate]: 'Candidate',
  [AppRoles.HiringManager]: 'Hiring Manager',
};

export function roleLabel(role: AppRole): string {
  return roleLabels[role];
}

// Where switching to this role as the active perspective should land — each role's
// first/primary NavMenu destination. Keeps the role switcher from leaving you on a
// screen that belongs to the role you just switched away from (e.g. a Sponsor's
// "My Projects" while viewing as Candidate).
const roleHomePaths: Record<AppRole, string> = {
  [AppRoles.Executive]: '/exec',
  [AppRoles.ProgramOps]: '/ops/dashboard',
  [AppRoles.Sponsor]: '/projects',
  [AppRoles.Candidate]: '/dashboard',
  [AppRoles.HiringManager]: '/pipeline',
};

export function roleHomePath(role: AppRole): string {
  return roleHomePaths[role];
}
