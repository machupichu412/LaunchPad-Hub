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
