import { PublicClientApplication, EventType, type AccountInfo } from '@azure/msal-browser';
import { msalConfig } from './msalConfig';

export const msalInstance = new PublicClientApplication(msalConfig);

export async function initializeMsal() {
  await msalInstance.initialize();

  // Required after loginRedirect(): without this, the redirect response is only
  // ever processed by MsalProvider's internal effect, which runs after first
  // render — so the singleton's "active account" (read directly by authedFetch,
  // outside React) can stay unset indefinitely even once useMsal()'s accounts
  // look signed-in. Resolving it here, before render, closes that race.
  const redirectResult = await msalInstance.handleRedirectPromise();

  const account = redirectResult?.account ?? msalInstance.getAllAccounts()[0];
  if (account) {
    msalInstance.setActiveAccount(account);
  }

  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      const account = (event.payload as { account: AccountInfo }).account;
      msalInstance.setActiveAccount(account);
    }
  });
}
