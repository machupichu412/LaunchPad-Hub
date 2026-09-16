// defineConfig from 'vitest/config', not 'vite' — it is the same function widened to accept
// the `test` block below, which vite's own types reject.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    // Without this the whole app was one 1.7MB chunk, so any change to any file
    // invalidated the entire download. Splitting the three large, rarely-updated
    // dependencies out means an app deploy only invalidates the app chunk, and
    // recharts is fetched only by the two dashboards that actually render charts.
    rollupOptions: {
      output: {
        // Function form, not the object map: Vite 8 bundles with rolldown, whose
        // manualChunks only accepts a function.
        manualChunks(id: string) {
          if (!id.includes('node_modules')) return
          if (id.includes('@fluentui')) return 'fluent'
          if (id.includes('@azure/msal')) return 'msal'
          if (/node_modules\/(react|react-dom|react-router|react-router-dom|scheduler)\//.test(id)) return 'react'
          return
        },
      },
    },
    // The default 500 kB warning is noise once Fluent is deliberately its own chunk;
    // this still flags anything unexpectedly large.
    chunkSizeWarningLimit: 700,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
})
