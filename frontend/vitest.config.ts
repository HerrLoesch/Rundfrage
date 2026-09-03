import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  define: {
    // Vuetify reads this at module scope; without it the components throw on import.
    'process.env.NODE_ENV': '"test"',
  },
  test: {
    environment: 'jsdom',
    include: ['tests/unit/**/*.spec.ts'],
    setupFiles: ['tests/support/setup.ts'],
    globals: true,
    server: { deps: { inline: ['vuetify'] } },
  },
})
