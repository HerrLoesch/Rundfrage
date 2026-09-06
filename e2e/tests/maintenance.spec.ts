import { test, expect } from '@playwright/test'
import { field } from '../support/fields'
import { ADMIN_PASSWORD, ADMIN_USER } from '../support/credentials'

/**
 * US2 from the outside, and constitution gate 3: this feature touches the answer flow, so the
 * participant path is verified end to end rather than only in integration tests.
 *
 * The suite leaves maintenance mode OFF whatever happens. A test that switched it on and failed
 * before switching it back would take every later test in the run down with it, and the failure
 * report would point at the wrong thing.
 */
test.describe('Maintenance mode (US2)', () => {
  async function signIn(page: import('@playwright/test').Page) {
    await page.goto('/admin')
    await field(page, 'sign-in-user').fill(ADMIN_USER)
    await field(page, 'sign-in-password').fill(ADMIN_PASSWORD)
    await page.getByTestId('sign-in-submit').click()
    await expect(page.getByTestId('maintenance-toggle')).toBeVisible()
  }

  async function setMaintenance(request: import('@playwright/test').APIRequestContext, enabled: boolean) {
    const response = await request.put('/api/v1/admin/maintenance', { data: { enabled } })
    expect(response.ok()).toBe(true)
  }

  test.afterEach(async ({ request }) => {
    await request.post('/api/v1/admin/session', {
      data: { user: ADMIN_USER, password: ADMIN_PASSWORD },
    })
    await setMaintenance(request, false)
  })

  test('a participant sees the notice instead of the poll, and the poll returns afterwards', async ({
    page,
    request,
  }) => {
    await signIn(page)

    await field(page, 'poll-title').fill('Wartungsprobe')
    await field(page, 'poll-days').fill('2027-12-01')
    await page.getByTestId('poll-submit').click()

    const link = await page.getByTestId('share-url').first().innerText()
    const path = new URL(link).pathname

    // Before: the poll is there.
    await page.goto(path)
    await expect(page.getByTestId('poll-view-title')).toBeVisible()

    await setMaintenance(request, true)

    await page.goto(path)
    await expect(page.getByTestId('maintenance-notice')).toBeVisible()
    await expect(page.getByTestId('poll-view-title')).toHaveCount(0)
    await expect(page.locator('body')).not.toContainText('Wartungsprobe')

    await setMaintenance(request, false)

    await page.goto(path)
    await expect(page.getByTestId('poll-view-title')).toBeVisible()
  })

  test('the notice is the same for a link that leads nowhere', async ({ request }) => {
    await request.post('/api/v1/admin/session', {
      data: { user: ADMIN_USER, password: ADMIN_PASSWORD },
    })
    await setMaintenance(request, true)

    const invented = await request.get('/api/v1/polls/aaaaaaaaaaaaaaaaaaaaaa')

    expect(invented.status()).toBe(503)
    expect(await invented.text()).toBe('{"code":"maintenance"}')
  })

  test('the health check stays green while maintenance is on', async ({ request }) => {
    // FR-031 and SC-011. If this went red, the orchestrator would roll the deployment back in
    // the middle of the maintenance window.
    await request.post('/api/v1/admin/session', {
      data: { user: ADMIN_USER, password: ADMIN_PASSWORD },
    })
    await setMaintenance(request, true)

    const health = await request.get('/api/v1/health')

    expect(health.status()).toBe(200)
  })

  test('the operator can still switch it off from the admin area', async ({ page, request }) => {
    // FR-028: without this the operator locks themselves out of their own maintenance window.
    await signIn(page)
    await setMaintenance(request, true)

    await page.reload()
    await expect(page.getByTestId('maintenance-banner')).toBeVisible()

    await page.getByTestId('maintenance-toggle').click()

    await expect(page.getByTestId('maintenance-banner')).toHaveCount(0)
  })
})
