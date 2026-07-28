import { PublicClientApplication, EventType, type AccountInfo } from '@azure/msal-browser';
import { msalConfig } from './msalConfig';

export const msalInstance = new PublicClientApplication(msalConfig);

export async function initializeMsal() {
  await msalInstance.initialize();

  const accounts = msalInstance.getAllAccounts();
  if (accounts.length > 0) {
    msalInstance.setActiveAccount(accounts[0]);
  }

  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      const account = (event.payload as { account: AccountInfo }).account;
      msalInstance.setActiveAccount(account);
    }
  });
}
