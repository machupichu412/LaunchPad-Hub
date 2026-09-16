import { useSyncExternalStore } from 'react';

const QUERY = '(prefers-reduced-motion: reduce)';

function subscribe(callback: () => void) {
  const mql = window.matchMedia(QUERY);
  mql.addEventListener('change', callback);
  return () => mql.removeEventListener('change', callback);
}

function getSnapshot() {
  return window.matchMedia(QUERY).matches;
}

/**
 * Mirrors the `@media (prefers-reduced-motion: reduce)` used throughout the app,
 * for the rare case that can only be decided in JS — such as whether to mount a
 * `<video>` at all, rather than just adjusting a CSS transition.
 */
export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, () => false);
}
