import { test, expect } from '@playwright/test'
import { field } from '../support/fields'
import { signIn, signInToPolls } from '../support/admin'
import { ADMIN_PASSWORD, ADMIN_USER } from '../support/credentials'

/**
 * FR-048 and SC-004 from the outside: the admin area is unreachable without signing in, and a
 * refusal discloses nothing.
 */
test.describe('Admin access', () => {
  test('the admin API refuses every request without a session', async ({ request }) => {
    for (const path of ['/api/v1/admin/polls']) {
      const response = await request.get(path)
      expect(response.status()).toBe(401)
      expect(await response.text()).toBe('{"code":"unauthorized"}')
    }
  })

  test('a refusal reveals nothing about what exists', async ({ request }) => {
    // Every admin route that exists refuses with the identical body (FR-002). The exhaustive
    // version of this lives in AdminAuthorizationTests, which discovers the routes from the
    // running endpoint table instead of listing them; this is the outside-the-process check.
    const listing = await request.get('/api/v1/admin/polls')

    expect(listing.status()).toBe(401)
    expect(await listing.text()).toBe('{"code":"unauthorized"}')
  })

  test('a wrong password is refused and says nothing about which half was wrong', async ({ request }) => {
    const wrongUser = await request.post('/api/v1/admin/session', {
      data: { user: 'definitely-not-the-operator', password: 'irrelevant' },
    })
    const wrongPassword = await request.post('/api/v1/admin/session', {
      data: { user: 'admin', password: 'definitely-wrong' },
    })

    expect(wrongUser.status()).toBe(401)
    expect(wrongPassword.status()).toBe(401)
    expect(await wrongUser.text()).toBe(await wrongPassword.text())
  })

  test('opening the admin page without a session lands on the sign-in form', async ({ page }) => {
    await page.goto('/admin')

    // The client-side guard only redirects; the refusal above is what actually protects the data.
    await expect(page.getByTestId('sign-in-form')).toBeVisible()

    // 007 FR-008: no session, so no areas to list. A navigation bar here would offer what the
    // server is about to refuse.
    await expect(page.getByTestId('admin-nav')).toHaveCount(0)
    await expect(page.getByTestId('admin-shell')).toHaveCount(0)
  })

  /**
   * 007 FR-011a. Signing in leads to the dashboard in every case - the area the operator was
   * refused from is deliberately not remembered, because carrying an intended destination across
   * an authentication boundary is machinery this feature has no second use for.
   */
  test('signing in lands on the dashboard, whatever was being asked for', async ({ page }) => {
    for (const asked of ['/admin/einstellungen', '/admin/terminfindungen', '/admin/gibt-es-nicht']) {
      await page.context().clearCookies()
      await page.goto(asked)
      await expect(page.getByTestId('sign-in-form')).toBeVisible()

      await field(page, 'sign-in-user').fill(ADMIN_USER)
      await field(page, 'sign-in-password').fill(ADMIN_PASSWORD)
      await page.getByTestId('sign-in-submit').click()

      await expect(page.getByTestId('dashboard'), asked).toBeVisible()
      await expect(page).toHaveURL(/\/admin$/)
    }
  })

  /**
   * 007 FR-012 and SC-007. A permanent drawer would occupy a phone-width screen, and a drawer
   * that stayed open over the content it navigated to would be worse than none.
   */
  test('the navigation is reachable on a 375-pixel screen and does not cover the content', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 720 })
    await signInToPolls(page)

    // Above the breakpoint the drawer is permanent and this control does not exist; here it must.
    const toggle = page.getByTestId('admin-nav-toggle')
    await expect(toggle).toBeVisible()

    await toggle.click()
    await expect(page.getByTestId('nav-settings')).toBeInViewport()
    await page.getByTestId('nav-settings').click()

    // Choosing a destination reveals it rather than leaving it behind the drawer.
    await expect(page.getByTestId('settings-maintenance')).toBeVisible()
    await expect(page.getByTestId('settings-maintenance')).toBeInViewport()

    // And the page itself never scrolls sideways.
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(overflow).toBeLessThanOrEqual(1)
  })

  /**
   * 007 FR-013 and SC-008. Every entry must say what it is and whether it is the one being shown,
   * and aria-current is the attribute that carries the second part.
   */
  test('the navigation is operable by keyboard and announces which area is current', async ({
    page,
  }) => {
    await signIn(page)

    // Exactly one entry is current, and it is the area actually shown.
    await expect(page.locator('[data-testid^="nav-"][aria-current="page"]')).toHaveCount(1)
    await expect(page.getByTestId('nav-dashboard')).toHaveAttribute('aria-current', 'page')

    // Reached and activated by keyboard alone.
    await page.getByTestId('nav-polls').focus()
    await expect(page.getByTestId('nav-polls')).toBeFocused()
    await page.keyboard.press('Enter')

    await expect(page.getByTestId('poll-create-toggle')).toBeVisible()
    await expect(page.getByTestId('nav-polls')).toHaveAttribute('aria-current', 'page')
    await expect(page.locator('[data-testid^="nav-"][aria-current="page"]')).toHaveCount(1)

    // Every entry carries a name.
    for (const entry of ['nav-dashboard', 'nav-polls', 'nav-settings']) {
      await expect(page.getByTestId(entry)).not.toBeEmpty()
    }
  })

  /**
   * The tab that outlived an update. Its shell asks for a chunk the new build no longer ships,
   * and before this was handled the click on settings did nothing at all - the navigation was
   * aborted and the only trace was a console error. Now the destination is loaded in full once,
   * which brings the current shell and the current chunk with it.
   */
  test('a tab whose shell predates an update still reaches settings on click', async ({ page }) => {
    // The stale shell's chunk name is gone exactly once; after the full load the fresh shell
    // asks for the current name, which the server has.
    let requests = 0
    await page.route(/\/assets\/SettingsView-.*\.js$/, (route) =>
      requests++ === 0 ? route.fulfill({ status: 404, body: 'gone' }) : route.continue(),
    )
    await signIn(page)

    await page.getByTestId('nav-settings').click()

    await expect(page.getByTestId('settings-maintenance')).toBeVisible()
    await expect(page).toHaveURL(/\/admin\/einstellungen$/)
    expect(requests).toBe(2)
  })

  test('the shell is never served from cache without asking the server first', async ({ request }) => {
    // index.html is the one file whose name stays the same while its content changes with every
    // build; cached heuristically, it is what made the click above fail. The hashed assets are
    // the opposite case and may be kept for good.
    const shell = await request.get('/admin')
    expect(shell.headers()['cache-control']).toBe('no-cache')

    const asset = (await shell.text()).match(/\/assets\/index-[^"]+\.js/)?.[0]
    expect(asset).toBeDefined()
    const chunk = await request.get(asset!)
    expect(chunk.headers()['cache-control']).toBe('public, max-age=31536000, immutable')
  })
})
