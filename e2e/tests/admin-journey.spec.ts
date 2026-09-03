import { test, expect } from '@playwright/test'
import { field, textarea } from '../support/fields'

/**
 * Taken from the environment, with no fallback.
 *
 * A default here would mean committing a working password to the repository - and one that
 * someone could plausibly deploy with. The application itself refuses to start without
 * credentials (FR-045); requiring the same of the suite that tests it is the consistent choice.
 */
function required(name: string): string {
  const value = process.env[name]
  if (!value) {
    throw new Error(
      `${name} is not set. The admin journey needs the operator credentials of the running ` +
        'instance:\n  export E2E_ADMIN_USER=... E2E_ADMIN_PASSWORD=...\n' +
        'They are whatever you put in .env - see README.md.',
    )
  }
  return value
}

const USER = required('E2E_ADMIN_USER')
const PASSWORD = required('E2E_ADMIN_PASSWORD')

/**
 * US1 from the outside, through the interface a person actually uses.
 *
 * The suite previously covered only the *unauthenticated* case, which is why two defects went
 * unnoticed: signing in successfully never navigated anywhere, and reloading the admin page
 * bounced back to the form despite a valid session cookie. A test that never signs in
 * successfully cannot see either.
 */
test.describe('Admin journey (US1)', () => {
  async function signIn(page: import('@playwright/test').Page) {
    await page.goto('/admin')
    await field(page, 'sign-in-user').fill(USER)
    await field(page, 'sign-in-password').fill(PASSWORD)
    await page.getByTestId('sign-in-submit').click()
  }

  test('the root address leads to the application', async ({ page }) => {
    // What a person sees when they open the address from the README.
    await page.goto('/')

    await expect(page.getByTestId('sign-in-form')).toBeVisible()
  })

  test('the diagnostic page is gone, and its address is not a hole', async ({ page }) => {
    // 003 FR-022. An unknown client-side route still serves the application shell rather than a
    // broken page - the address simply no longer means anything.
    await page.goto('/status')

    await expect(page.getByTestId('database-state')).toHaveCount(0)
    await expect(page.getByTestId('backend-message')).toHaveCount(0)
  })

  test('signing in reaches the admin area', async ({ page }) => {
    await signIn(page)

    await expect(page.getByTestId('poll-form')).toBeVisible()
    await expect(page.getByTestId('sign-in-form')).toBeHidden()
  })

  test('the admin area survives a page reload', async ({ page }) => {
    // The session lives in an HttpOnly cookie, so a reload is still authenticated. Anything
    // that decides otherwise is reading client-side state that a reload has thrown away.
    await signIn(page)
    await expect(page.getByTestId('poll-form')).toBeVisible()

    await page.reload()

    await expect(page.getByTestId('poll-form')).toBeVisible()
    await expect(page.getByTestId('sign-in-form')).toBeHidden()
  })

  test('a poll can be created and shows its link and deadline', async ({ page }) => {
    await signIn(page)
    await expect(page.getByTestId('poll-form')).toBeVisible()

    const title = `Grillabend ${Date.now()}`
    await field(page, 'poll-title').fill(title)
    await textarea(page, 'poll-message').fill('Wann passt es euch?')

    for (const day of ['2026-11-20', '2026-11-18']) {
      await field(page, 'poll-day-input').fill(day)
      await page.getByTestId('poll-add-day').click()
    }

    // FR-013: chronological regardless of the order they were added.
    const shown = await page.getByTestId('poll-day').evaluateAll((els) =>
      els.map((e) => e.getAttribute('data-date')),
    )
    expect(shown).toEqual(['2026-11-18', '2026-11-20'])

    await page.getByTestId('poll-submit').click()

    await expect(page.getByTestId('poll-share-link')).toContainText('/u/')
    await expect(page.getByTestId('poll-retention')).not.toBeEmpty()
  })

  test('signing out returns to the form and the admin area is refused again', async ({ page }) => {
    await signIn(page)
    await expect(page.getByTestId('poll-form')).toBeVisible()

    await page.getByTestId('sign-out').click()

    await expect(page.getByTestId('sign-in-form')).toBeVisible()

    const afterSignOut = await page.request.get('/api/v1/admin/polls')
    expect(afterSignOut.status()).toBe(401)
  })
})
