import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { webDarkTheme, webLightTheme, type Theme } from '@fluentui/react-components';

export type ThemeMode = 'light' | 'dark';

const STORAGE_KEY = 'launchpad-theme-mode';

/** Exported so the pre-mount loading screen (main.tsx) can pick the same theme
 * before ThemeModeProvider exists, avoiding a flash of the wrong mode. */
export function getInitialMode(): ThemeMode {
  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === 'light' || stored === 'dark') return stored;
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

type ThemeModeState = {
  mode: ThemeMode;
  theme: Theme;
  toggleMode: () => void;
};

const ThemeModeContext = createContext<ThemeModeState | undefined>(undefined);

/**
 * Light/dark parity is a first-class part of the Fluent 2 web theme (webLightTheme/
 * webDarkTheme ship as a pair) and every Microsoft 365 surface respects it — an
 * internal tool with no theme toggle reads as un-Microsoft regardless of which
 * tokens it uses elsewhere. Defaults to the OS preference, then remembers the
 * user's explicit choice.
 */
export function ThemeModeProvider({ children }: { children: ReactNode }) {
  const [mode, setMode] = useState<ThemeMode>(getInitialMode);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, mode);
  }, [mode]);

  const value = useMemo<ThemeModeState>(
    () => ({
      mode,
      theme: mode === 'dark' ? webDarkTheme : webLightTheme,
      toggleMode: () => setMode((current) => (current === 'dark' ? 'light' : 'dark')),
    }),
    [mode],
  );

  return <ThemeModeContext.Provider value={value}>{children}</ThemeModeContext.Provider>;
}

export function useThemeMode(): ThemeModeState {
  const ctx = useContext(ThemeModeContext);
  if (!ctx) throw new Error('useThemeMode must be used within a ThemeModeProvider');
  return ctx;
}
