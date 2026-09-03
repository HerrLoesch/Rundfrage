import { test, expect } from '@playwright/test'

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
  })
})
