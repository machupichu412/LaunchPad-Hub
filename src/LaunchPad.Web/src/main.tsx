import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { MsalProvider } from '@azure/msal-react';
import { QueryClientProvider } from '@tanstack/react-query';
import { FluentProvider, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import './index.css';
import App from './App.tsx';
import { msalInstance, initializeMsal } from './auth/msalInstance';
import { queryClient } from './api/queryClient';
import { getInitialMode } from './theme/ThemeModeContext';
import { InitialLoadingScreen } from './components/InitialLoadingScreen';

const root = createRoot(document.getElementById('root')!);

// initializeMsal() must fully resolve before the real app mounts (see its own
// comment — MsalProvider's redirect handling runs after first render, which is
// too late for authedFetch's synchronous getActiveAccount() read). Without
// something rendered in the meantime, that wait is a blank white page —
// render a themed loading screen first, then swap it for the real app.
root.render(
  <StrictMode>
    <FluentProvider theme={getInitialMode() === 'dark' ? webDarkTheme : webLightTheme}>
      <InitialLoadingScreen />
    </FluentProvider>
  </StrictMode>,
);

await initializeMsal();

root.render(
  <StrictMode>
    <MsalProvider instance={msalInstance}>
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </MsalProvider>
  </StrictMode>,
);
