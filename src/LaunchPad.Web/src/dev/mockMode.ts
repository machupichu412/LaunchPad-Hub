/**
 * Local-only design-review bypass. `import.meta.env.DEV` is a compile-time constant
 * Vite hard-codes to `false` for any production build (`vite build`), so every branch
 * gated on `isMockMode` is dead code that gets stripped by the production bundler —
 * this cannot exist in a deployed build regardless of env vars. Even in dev, it stays
 * off unless VITE_MOCK_MODE=true is set explicitly (see .env.local, gitignored).
 *
 * When on: no Entra ID sign-in, no MSAL token acquisition, and no network call ever
 * reaches the real API — authedFetch (see mockApi.ts) is short-circuited to synthetic,
 * in-memory fixture data before it can construct a request. This exists purely to let
 * visual/design work be reviewed in a browser without live Entra credentials; it has
 * zero effect on the API's own authorization, which independently enforces every
 * request regardless of what the SPA believes about itself.
 */
export const isMockMode = import.meta.env.DEV && import.meta.env.VITE_MOCK_MODE === 'true';
