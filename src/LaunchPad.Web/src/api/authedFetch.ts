import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { apiRequest } from '../auth/msalConfig';
import { msalInstance } from '../auth/msalInstance';
import { isMockMode } from '../dev/mockMode';
import { resolveMock } from '../dev/mockApi';
import { PERSONA_HEADER, getActivePersona, isPersonaMode } from '../dev/devPersonas';

const API_TIMEOUT_MS = Number(import.meta.env.VITE_API_TIMEOUT_MS ?? 30_000);

/**
 * Combines the caller's abort signal (if any) with a timeout, rather than replacing it —
 * replacing it would silently break cancel-on-unmount, which fails quietly.
 * Exported for tests.
 */
export function composeRequestSignal(
  callerSignal: AbortSignal | null | undefined,
  timeoutMs: number = API_TIMEOUT_MS,
): AbortSignal {
  const timeoutSignal = AbortSignal.timeout(timeoutMs);
  if (!callerSignal) return timeoutSignal;

  // Hand-rolled rather than AbortSignal.any: that landed in Safari only in 17.4, which is
  // recent enough to be worth not depending on, and jsdom does not implement it at all.
  const controller = new AbortController();
  for (const source of [callerSignal, timeoutSignal]) {
    if (source.aborted) {
      controller.abort(source.reason);
      break;
    }
    source.addEventListener('abort', () => controller.abort(source.reason), { once: true });
  }

  return controller.signal;
}

/**
 * The single place a bearer token is acquired — no call site should acquire tokens
 * itself. See launchpad-build-guide.md §7.2.
 */
export async function authedFetch(input: string, init: RequestInit = {}): Promise<Response> {
  // Mock mode (see dev/mockMode.ts): resolved entirely in-memory, before token
  // acquisition or the real fetch() call are ever reached — no request leaves the
  // browser. This branch does not exist in a production build.
  if (isMockMode) {
    const method = init.method ?? 'GET';
    const bodyForMock = typeof init.body === 'string' ? safeJsonParse(init.body) : undefined;
    const { status, body } = await resolveMock(method, input, bodyForMock);
    return new Response(body === null || body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  // Persona mode (see dev/devPersonas.ts): the real local API, but identity comes from a
  // header the API only honours in Development. Stripped from production builds.
  if (isPersonaMode) {
    const persona = getActivePersona();
    if (!persona) throw new Error('No persona selected');
    const isFormData = init.body instanceof FormData;
    return fetch(`${import.meta.env.VITE_API_BASE_URL ?? ''}${input}`, {
      ...init,
      signal: composeRequestSignal(init.signal),
      headers: {
        ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
        ...init.headers,
        [PERSONA_HEADER]: persona.key,
      },
    });
  }

  // Falls back to the first cached account if none is explicitly "active" yet —
  // see the race explained in msalInstance.ts's initializeMsal.
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  if (!account) throw new Error('No active account');

  let accessToken: string;
  try {
    const result = await msalInstance.acquireTokenSilent({ ...apiRequest, account });
    accessToken = result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      const result = await msalInstance.acquireTokenPopup(apiRequest);
      accessToken = result.accessToken;
    } else {
      throw error;
    }
  }

  const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '';

  // A FormData body (multipart deliverable uploads, see api/assignments.ts) must never get
  // an explicit Content-Type — the browser sets its own boundary-bearing multipart value,
  // and pre-setting application/json here would strip that boundary and break the upload.
  const isFormData = init.body instanceof FormData;

  // Without a deadline a hung request never settles, and the view that awaits it shows its
  // loading state forever — there is no browser-level fetch timeout.
  const signal = composeRequestSignal(init.signal);

  const response = await fetch(`${baseUrl}${input}`, {
    ...init,
    signal,
    headers: {
      // Defaults first so a caller-supplied Content-Type (e.g. the raw
      // image/jpeg body avatar uploads send, see api/avatar.ts) overrides it —
      // only Authorization is never overridable by a call site.
      ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
      ...init.headers,
      Authorization: `Bearer ${accessToken}`,
    },
  });

  // 401 means the JWT itself failed validation (wrong audience/issuer, expired, wrong
  // tenant), not a missing-role 403. The WWW-Authenticate header from ASP.NET Core's
  // JwtBearer handler names the exact reason, which is enough to tell those apart.
  //
  // This used to also decode and log the access token's own claims. That printed token
  // contents into the browser console, where anything with console access can read them
  // and any error-reporting tool that captures console output would ship them off-box.
  if (response.status === 401) {
    console.warn('authedFetch: 401 from', input, '\nWWW-Authenticate:', response.headers.get('www-authenticate'));
  }

  return response;
}

function safeJsonParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}
