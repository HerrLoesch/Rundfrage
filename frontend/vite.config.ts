import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// The dev server proxies the API prefix to the containerised backend (FR-003b).
// The browser therefore sees a single origin in development too, so no CORS
// configuration exists in either mode (FR-003a, research.md R-10).
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
  },
})
