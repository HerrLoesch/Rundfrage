import { defineConfig, devices } from '@playwright/test'

// End-to-end tests run against the real container set started by
// `docker compose up`, not against the Vite dev server (FR-021).
//
// 127.0.0.1 rather than `localhost`: compose.yaml publishes the port on the loopback address,
// and that binding is IPv4-only, while `localhost` resolves to ::1 first on most machines.
export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:8080',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
