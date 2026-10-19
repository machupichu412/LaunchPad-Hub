import { AppRoles, type AppRole } from '../auth/roles';
import { isMockMode } from './mockMode';

/**
 * Local-only multi-role testing (see LocalDemo/DevPersonaAuthHandler.cs in the API).
 * Same compile-time gating as mockMode.ts: import.meta.env.DEV is false in every
 * production build, so this is dead code there. Mutually exclusive with mock mode —
 * persona mode talks to the real local API, mock mode never does.
 *
 * The chosen persona lives in sessionStorage, which is per tab: each browser tab is an
 * independent signed-in user, which is what lets one browser drive a Sponsor and an
 * Ops approver side by side.
 */
export const isPersonaMode =
  import.meta.env.DEV && import.meta.env.VITE_DEV_PERSONAS === 'true' && !isMockMode;

export const PERSONA_HEADER = 'X-Dev-Persona';
const STORAGE_KEY = 'launchpad.devPersona';

export type DevPersona = { key: string; displayName: string; roles: AppRole[]; note: string };

// Mirrors src/LaunchPad.Api/LocalDemo/DevPersonas.cs — the API is the authority on
// roles; this copy only labels the picker and shapes navigation.
export const DEV_PERSONAS: DevPersona[] = [
  { key: 'ops', displayName: 'Olivia Ops', roles: [AppRoles.ProgramOps], note: 'Program Ops' },
  { key: 'exec', displayName: 'Evan Exec', roles: [AppRoles.Executive], note: 'Executive' },
  { key: 'hm', displayName: 'Harper Hiring', roles: [AppRoles.HiringManager], note: 'Hiring Manager' },
  { key: 'sponsor', displayName: 'Sam Sponsor', roles: [AppRoles.Sponsor], note: 'Sponsor, owns the demo projects' },
  { key: 'sponsor2', displayName: 'Priya Shah', roles: [AppRoles.Sponsor], note: 'Sponsor, a different organization' },
  { key: 'sponsor-new', displayName: 'Nia Newsponsor', roles: [AppRoles.Sponsor], note: 'Sponsor with no profile yet' },
  { key: 'cand1', displayName: 'Jordan Rivera', roles: [AppRoles.Candidate], note: 'Candidate, spring cohort' },
  { key: 'cand2', displayName: 'Casey Kim', roles: [AppRoles.Candidate], note: 'Candidate, spring cohort' },
  { key: 'cand3', displayName: 'Morgan Lee', roles: [AppRoles.Candidate], note: 'Candidate, spring cohort' },
  { key: 'cand-c2', displayName: 'Alex Torres', roles: [AppRoles.Candidate], note: 'Candidate, fall cohort' },
  { key: 'cand-new', displayName: 'Riley Newcandidate', roles: [AppRoles.Candidate], note: 'Candidate with no profile yet' },
  { key: 'ops-sponsor', displayName: 'Dana Dualrole', roles: [AppRoles.ProgramOps, AppRoles.Sponsor], note: 'Program Ops and Sponsor' },
  { key: 'norole', displayName: 'Nobody Noroles', roles: [], note: 'Signed in with no roles' },
];

export function getActivePersona(): DevPersona | null {
  if (!isPersonaMode) return null;
  try {
    const key = sessionStorage.getItem(STORAGE_KEY);
    return DEV_PERSONAS.find((p) => p.key === key) ?? null;
  } catch {
    return null;
  }
}

export function setActivePersona(key: string | null) {
  try {
    if (key) sessionStorage.setItem(STORAGE_KEY, key);
    else sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    // Storage blocked: the picker simply shows again on reload.
  }
  // A full reload, not a state update: every cached query belongs to the previous
  // identity, and a reload is the one way to guarantee none of it survives.
  window.location.assign('/');
}
