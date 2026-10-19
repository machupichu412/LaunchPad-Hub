import { afterEach, describe, expect, it, vi } from 'vitest';

/**
 * isPersonaMode is read at module load, so each case stubs env first and imports fresh.
 */
describe('devPersonas', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
    sessionStorage.clear();
  });

  it('is off unless VITE_DEV_PERSONAS is explicitly true', async () => {
    vi.stubEnv('VITE_DEV_PERSONAS', '');
    const { isPersonaMode, getActivePersona } = await import('./devPersonas');
    sessionStorage.setItem('launchpad.devPersona', 'ops');
    expect(isPersonaMode).toBe(false);
    expect(getActivePersona()).toBeNull();
  });

  it('cannot be on at the same time as mock mode', async () => {
    vi.stubEnv('VITE_DEV_PERSONAS', 'true');
    vi.stubEnv('VITE_MOCK_MODE', 'true');
    const { isPersonaMode } = await import('./devPersonas');
    expect(isPersonaMode).toBe(false);
  });

  it('sends the tab’s persona as a header and no bearer token', async () => {
    vi.stubEnv('VITE_DEV_PERSONAS', 'true');
    vi.stubEnv('VITE_MOCK_MODE', 'false');
    sessionStorage.setItem('launchpad.devPersona', 'sponsor2');
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}'));
    vi.stubGlobal('fetch', fetchMock);

    const { authedFetch } = await import('../api/authedFetch');
    await authedFetch('/api/me');

    const headers = fetchMock.mock.calls[0][1].headers as Record<string, string>;
    expect(headers['X-Dev-Persona']).toBe('sponsor2');
    expect(headers.Authorization).toBeUndefined();
    vi.unstubAllGlobals();
  });

  it('ignores an unknown persona key left in storage', async () => {
    vi.stubEnv('VITE_DEV_PERSONAS', 'true');
    vi.stubEnv('VITE_MOCK_MODE', 'false');
    sessionStorage.setItem('launchpad.devPersona', 'admin');
    const { getActivePersona } = await import('./devPersonas');
    expect(getActivePersona()).toBeNull();
  });
});
