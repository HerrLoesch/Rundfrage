import { test, expect } from '@playwright/test'
import { execSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')

function compose(args: string): void {
  execSync(`docker compose ${args}`, { cwd: repoRoot, stdio: 'pipe' })
}

async function waitForDatabaseState(page: import('@playwright/test').Page, expected: string) {
  await expect(async () => {
    await page.reload()
    await expect(page.getByTestId('database-state')).toHaveAttribute('data-state', expected)
  }).toPass({ timeout: 60_000, intervals: [1000, 2000, 3000] })
}

test.describe('Database status (US2)', () => {
  test.afterAll(() => {
    // Leave the system in a working state whatever happened above.
    compose('start db')
  })

  test('reports the database as reachable while it is up', async ({ page }) => {
    compose('start db')
    await page.goto('/')
    await waitForDatabaseState(page, 'reachable')
    await expect(page.getByTestId('database-state')).toHaveText('Datenbank erreichbar')
  })

  test('reports the failure, and still renders, while the database is down', async ({ page }) => {
    // Acceptance scenario 2.2 - the page must never go blank or error out (FR-011, SC-004).
    compose('stop db')

    await page.goto('/')
    await expect(page.getByTestId('app-title')).toBeVisible()
    await expect(page.getByTestId('backend-message')).toBeVisible()
    await waitForDatabaseState(page, 'unreachable')
  })

  test('recovers on reload once the database is back, with no restart', async ({ page }) => {
    // SC-005
    compose('stop db')
    await page.goto('/')
    await waitForDatabaseState(page, 'unreachable')

    compose('start db')
    await waitForDatabaseState(page, 'reachable')
  })

  test('distinguishes an unreachable backend from an unreachable database', async ({ page }) => {
    // research.md R-4: the third state is derived client-side. Simulated by making the status
    // request itself fail while the page is still served - stopping the app container would
    // prevent the page from loading at all, so nothing could be asserted.
    compose('start db')
    await page.route('**/api/v1/status/database', (route) => route.abort())

    await page.goto('/')

    const state = page.getByTestId('database-state')
    await expect(state).toHaveAttribute('data-state', 'backendUnreachable')
    await expect(state).toHaveText('Backend nicht erreichbar')
  })

  test('the status endpoint answers 200 even when the database is down', async ({ request }) => {
    // A 503 would be read by the frontend as "backend unreachable" (contracts/openapi.yaml).
    compose('stop db')

    const response = await request.get('/api/v1/status/database')
    expect(response.status()).toBe(200)
    expect((await response.json()).state).toBe('unreachable')

    compose('start db')
  })
})
