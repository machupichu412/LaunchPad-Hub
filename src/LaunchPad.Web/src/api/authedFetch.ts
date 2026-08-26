import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { apiRequest } from '../auth/msalConfig';
import { msalInstance } from '../auth/msalInstance';
import { isMockMode } from '../dev/mockMode';
import { resolveMock } from '../dev/mockApi';

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

  const response = await fetch(`${baseUrl}${input}`, {
    ...init,
    headers: {
      // Defaults first so a caller-supplied Content-Type (e.g. the raw
      // image/jpeg body avatar uploads send, see api/avatar.ts) overrides it —
      // only Authorization is never overridable by a call site.
      ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
      ...init.headers,
      Authorization: `Bearer ${accessToken}`,
    },
  });

  // Temporary diagnostic — 401 means the JWT itself failed validation (wrong
  // audience/issuer, expired, wrong tenant), not a missing-role 403. The
  // WWW-Authenticate header from ASP.NET Core's JwtBearer handler names the exact
  // reason; the decoded token claims show what was actually sent. Remove once the
  // current 401 investigation is resolved.
  if (response.status === 401) {
    console.warn('authedFetch: 401 from', input, '\nWWW-Authenticate:', response.headers.get('www-authenticate'));
    try {
      const payload = JSON.parse(atob(accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
      console.warn('authedFetch: decoded access token claims:', { aud: payload.aud, iss: payload.iss, tid: payload.tid, exp: payload.exp ? new Date(payload.exp * 1000).toISOString() : undefined, roles: payload.roles });
    } catch {
      console.warn('authedFetch: could not decode access token');
    }
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
