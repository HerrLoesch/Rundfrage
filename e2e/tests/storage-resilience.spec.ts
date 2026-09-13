import { test, expect } from '@playwright/test'
import { execSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import { field } from '../support/fields'
import { revealPollForm, signInToPolls } from '../support/admin'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')

function compose(args: string): void {
  execSync(`docker compose ${args}`, { cwd: repoRoot, stdio: 'pipe' })
}

/**
 * What feature 001's walking-skeleton and database-status suites were really proving, now
 * pointed at the product (003 FR-024a, FR-024b).
 *
 * Those two suites went away with the diagnostic page they exercised. Their subject disappeared;
 * their assertions did not, and deleting a test's subject is not a reason to delete what it
 * established. Three things were worth keeping, and all three are about the product rather than
 * about a page built to demonstrate it:
 *
 *   - every asset comes from the application's own origin (constitution Principle IV),
 *   - the application keeps serving when its storage cannot be read, rather than going blank,
 *   - and it recovers on a reload, with no restart.
 */
test.describe('Storage resilience (US1)', () => {
  /**
   * 007: signing in lands on the dashboard, so reaching a control is now "sign in, then go to the
   * area it lives in". The shared helper carries that so a future rearrangement is one edit.
   */
  async function signIn(page: import('@playwright/test').Page) {
    await signInToPolls(page)
    await revealPollForm(page)
  }

  test('the application is served from a single origin', async ({ page }) => {
    // 001 FR-003a, and Principle IV: no CDN, no external font, no analytics. Inherited from the
    // walking-skeleton suite, which asserted it against the diagnostic page - a page that loaded
    // almost nothing, and so was the weakest place in the system to assert it.
    const expectedOrigin = new URL(test.info().project.use.baseURL ?? 'http://localhost:8080')
      .origin

    const crossOrigin: string[] = []
    page.on('request', (r) => {
      if (new URL(r.url()).origin !== expectedOrigin) crossOrigin.push(r.url())
    })

    await signIn(page)
    await page.waitForLoadState('networkidle')

    expect(crossOrigin).toEqual([])
  })

  test('polls and answers survive a restart of the whole system', async ({ page }) => {
    // SC-004. The data is a file in a mounted volume now, so this is the assertion that the
    // volume is really mounted where the application writes - a mistake that would otherwise
    // surface as an empty admin area on the day of the first restart.
    await signIn(page)

    const title = `Neustart ${Date.now()}`
    await field(page, 'poll-title').fill(title)
    await page.getByTestId('poll-add-day').click()
    await page.getByTestId('poll-submit').click()
    await expect(page.getByTestId('poll-list-item').filter({ hasText: title })).toBeVisible()

    compose('restart app')

    await expect(async () => {
      await page.goto('/admin/terminfindungen')
      await expect(page.getByTestId('poll-list-item').filter({ hasText: title })).toBeVisible()
    }).toPass({ timeout: 60_000, intervals: [1000, 2000, 3000] })
  })

  test('the admin area says so when storage cannot be read, and recovers on reload', async ({
    page,
  }) => {
    // FR-024a and SC-012, inherited from the database-status suite. The failure is injected at
    // the request rather than by breaking the container: stopping the application would prevent
    // the page from loading at all, so nothing could be asserted about what it shows.
    //
    // What matters is the distinction. An empty list and an unreadable store look identical and
    // mean opposite things - "you have not created any yet" against "your data cannot be reached
    // right now" - and showing the first when the second is true is a quiet lie.
    await signIn(page)

    await page.route('**/api/v1/admin/polls', (route) => route.abort())
    await page.goto('/admin/terminfindungen')

    await expect(page.getByTestId('storage-unavailable')).toBeVisible()
    await expect(page.getByTestId('poll-list-empty')).toHaveCount(0)

    // SC-005 of feature 001, kept: recovery needs a reload, never a restart.
    await page.unrouteAll()
    await page.reload()
    await expect(page.getByTestId('storage-unavailable')).toHaveCount(0)
    await expect(page.getByTestId('poll-create-toggle')).toBeVisible()
  })

  /**
   * 007 FR-032 and SC-009. The same distinction, and sharper here than anywhere else: an empty
   * list is ambiguous, but a figure reading 0 is a positive claim about the data. So when the
   * store cannot be read the dashboard must show no figure at all.
   */
  test('the dashboard shows no figure at all when the store cannot be read', async ({ page }) => {
    await signIn(page)

    await page.route('**/api/v1/admin/dashboard', (route) => route.abort())
    await page.goto('/admin')

    await expect(page.getByTestId('storage-unavailable')).toBeVisible()

    for (const tile of ['stat-polls', 'stat-responses', 'stat-unanswered', 'stat-distribution']) {
      await expect(page.getByTestId(tile)).toHaveCount(0)
    }

    await page.unrouteAll()
    await page.reload()
    await expect(page.getByTestId('stat-polls')).toBeVisible()
  })
})
