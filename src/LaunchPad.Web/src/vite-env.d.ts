/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_TENANT_ID: string;
  readonly VITE_SPA_CLIENT_ID: string;
  readonly VITE_API_CLIENT_ID: string;
  readonly VITE_API_BASE_URL: string;
  readonly VITE_MOCK_MODE?: string;
  readonly VITE_DEV_PERSONAS?: string;
  readonly VITE_API_TIMEOUT_MS?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
