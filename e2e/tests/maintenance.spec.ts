import { test, expect } from '@playwright/test'
import { field } from '../support/fields'
import { ADMIN_PASSWORD, ADMIN_USER } from '../support/credentials'
import { gotoPolls, revealPollForm, signInToPolls, signInToSettings } from '../support/admin'

/**
 * US2 from the outside, and constitution gate 3: this feature touches the answer flow, so the
 * participant path is verified end to end rather than only in integration tests.
 *
 * The suite leaves maintenance mode OFF whatever happens. A test that switched it on and failed
 * before switching it back would take every later test in the run down with it, and the failure
 * report would point at the wrong thing.
 */
test.describe('Maintenance mode (US2)', () => {
  /**
   * 007: signing in lands on the dashboard, so reaching a control is now "sign in, then go to the
   * area it lives in". The shared helper carries that so a future rearrangement is one edit.
   */
  async function signIn(page: import('@playwright/test').Page) {
    await signInToSettings(page)
    await expect(page.getByTestId('maintenance-toggle')).toBeVisible()
  }

  /**
   * Signs in first, every time. The `request` fixture is its own context and does not share the
   * page's cookies, so a test that signed in through the browser has no session here - which is
   * exactly how this suite failed the first time it ran.
   */
  async function setMaintenance(request: import('@playwright/test').APIRequestContext, enabled: boolean) {
    await request.post('/api/v1/admin/session', {
      data: { user: ADMIN_USER, password: ADMIN_PASSWORD },
    })
    const response = await request.put('/api/v1/admin/maintenance', { data: { enabled } })
    expect(response.ok()).toBe(true)
  }

  test.afterEach(async ({ request }) => {
    await setMaintenance(request, false)
  })

  test('a participant sees the notice instead of the poll, and the poll returns afterwards', async ({
    page,
    request,
  }) => {
    // Creating a poll needs the poll area; the maintenance switch this suite otherwise drives
    // lives in settings (007 FR-019).
    await signInToPolls(page)
    await revealPollForm(page)

    await field(page, 'poll-title').fill('Wartungsprobe')
    await field(page, 'poll-day-input').fill('2027-12-01')
    await page.getByTestId('poll-add-day').click()
    await page.getByTestId('poll-submit').click()

    const link = await page.getByTestId('poll-share-link').first().innerText()
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
    await setMaintenance(request, true)

    const invented = await request.get('/api/v1/polls/aaaaaaaaaaaaaaaaaaaaaa')

    expect(invented.status()).toBe(503)
    expect(await invented.text()).toBe('{"code":"maintenance"}')
  })

  test('the health check stays green while maintenance is on', async ({ request }) => {
    // FR-031 and SC-011. If this went red, the orchestrator would roll the deployment back in
    // the middle of the maintenance window.
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

  /**
   * 007 FR-026 and SC-012 - the assertion the old arrangement could not make.
   *
   * The banner used to be rendered by the switch, so it was only ever on screen where the switch
   * was. Moving the switch to settings would have taken the warning out of every area the
   * operator actually works in, and the documented failure mode of this feature is forgetting to
   * switch it back off. The banner therefore belongs to the shell.
   */
  test('the warning is on screen in every admin area, not only where the switch is', async ({
    page,
    request,
  }) => {
    await signIn(page)
    await setMaintenance(request, true)

    // Settings, where the switch lives.
    await page.reload()
    await expect(page.getByTestId('maintenance-banner')).toBeVisible()

    // The poll area, where it does not.
    await gotoPolls(page)
    await expect(page.getByTestId('maintenance-toggle')).toHaveCount(0)
    await expect(page.getByTestId('maintenance-banner')).toBeVisible()

    // And the dashboard.
    await page.getByTestId('nav-dashboard').click()
    await expect(page.getByTestId('stat-maintenance')).toBeVisible()
    await expect(page.getByTestId('maintenance-banner')).toBeVisible()

    // Switched off from settings, it clears everywhere (FR-026b).
    await setMaintenance(request, false)
    await page.reload()
    await expect(page.getByTestId('maintenance-banner')).toHaveCount(0)
  })
})
