import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { apiRequest } from '../auth/msalConfig';
import { msalInstance } from '../auth/msalInstance';

/**
 * The single place a bearer token is acquired — no call site should acquire tokens
 * itself. See launchpad-build-guide.md §7.2.
 */
export async function authedFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const account = msalInstance.getActiveAccount();
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

  return fetch(`${baseUrl}${input}`, {
    ...init,
    headers: {
      ...init.headers,
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
    },
  });
}
