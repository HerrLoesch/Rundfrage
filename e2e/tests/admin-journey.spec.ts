import { test, expect } from '@playwright/test'
import { field, textarea } from '../support/fields'
import { gotoPolls, revealPollForm, signIn as sharedSignIn } from '../support/admin'

/**
 * US1 from the outside, through the interface a person actually uses.
 *
 * The suite previously covered only the *unauthenticated* case, which is why two defects went
 * unnoticed: signing in successfully never navigated anywhere, and reloading the admin page
 * bounced back to the form despite a valid session cookie. A test that never signs in
 * successfully cannot see either.
 */
test.describe('Admin journey (US1)', () => {
  /**
   * 007: signing in lands on the dashboard, so reaching a control is now "sign in, then go to the
   * area it lives in". The shared helper carries that so a future rearrangement is one edit.
   */
  async function signIn(page: import('@playwright/test').Page) {
    await sharedSignIn(page)
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

  test('the wordmark sits on the middle of the bar', async ({ page }) => {
    // Measured rather than eyeballed, and only a real browser can do it. It was four pixels
    // high: v-app-bar-title is a block with a line-height, so an image inside it sits on the
    // text baseline instead of the middle - invisible to a component test, which computes no
    // layout at all.
    await page.goto('/')
    await page.getByTestId('brand').waitFor()

    const gaps = await page.evaluate(() => {
      const bar = document.querySelector('.v-toolbar')!.getBoundingClientRect()
      const logo = document.querySelector('[data-testid="brand"] img')!.getBoundingClientRect()
      return { above: logo.top - bar.top, below: bar.bottom - logo.bottom }
    })

    expect(Math.abs(gaps.above - gaps.below)).toBeLessThanOrEqual(1)
  })

  test('the wordmark is really there, not a broken image', async ({ page }) => {
    // The one failure a component test cannot see: the file resolves at build time and 404s at
    // run time, and the page still renders - with an empty box where the brand should be.
    await page.goto('/')

    const logo = page.getByTestId('brand').locator('img')
    await expect(logo).toBeVisible()

    const drawn = await logo.evaluate((img) => {
      const image = img as HTMLImageElement
      return image.complete && image.naturalWidth > 0
    })
    expect(drawn).toBe(true)
  })

  test('signing in reaches the admin area', async ({ page }) => {
    await signIn(page)
    await gotoPolls(page)

    await revealPollForm(page)
    await expect(page.getByTestId('sign-in-form')).toBeHidden()
  })

  test('the admin area survives a page reload', async ({ page }) => {
    // The session lives in an HttpOnly cookie, so a reload is still authenticated. Anything
    // that decides otherwise is reading client-side state that a reload has thrown away.
    await signIn(page)
    await gotoPolls(page)
    await expect(page).toHaveURL(/\/admin\/terminfindungen$/)

    await page.reload()

    // 007 SC-006: the address returns the operator to the same area, not to the start page.
    await expect(page).toHaveURL(/\/admin\/terminfindungen$/)
    await expect(page.getByTestId('poll-create-toggle')).toBeVisible()
    await expect(page.getByTestId('sign-in-form')).toBeHidden()
  })

  test('a poll can be created and shows its link and deadline', async ({ page }) => {
    await signIn(page)
    await gotoPolls(page)
    await revealPollForm(page)

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

    // 007 FR-010: sign-out belongs to the shell, so it is reachable from the landing area without
    // going anywhere first.
    await page.getByTestId('sign-out').click()

    await expect(page.getByTestId('sign-in-form')).toBeVisible()

    const afterSignOut = await page.request.get('/api/v1/admin/polls')
    expect(afterSignOut.status()).toBe(401)
  })

  /**
   * 007 FR-027 and FR-030. The dashboard is where signing in leads, and its figures are read on
   * entering the area rather than cached - so a change made elsewhere shows up on return without
   * a manual reload.
   */
  test('the dashboard is the landing area and its figures follow what changes', async ({
    page,
  }) => {
    await signIn(page)

    await expect(page.getByTestId('dashboard')).toBeVisible()
    await expect(page.getByTestId('nav-dashboard')).toHaveAttribute('aria-current', 'page')

    const polls = () => page.getByTestId('stat-polls')
    const countOf = async (locator: ReturnType<typeof page.getByTestId>) =>
      Number((await locator.textContent())!.replace(/\D+/g, ''))

    /**
     * Retries rather than reads once. The store keeps the previous figures while the next read is
     * in flight - deliberately, so returning to the dashboard does not flash a loading state - so
     * a single read can catch the old number before the new one lands.
     */
    const expectPollCount = async (expected: number) =>
      expect(async () => expect(await countOf(polls())).toBe(expected)).toPass({ timeout: 10_000 })

    const before = await countOf(polls())

    const title = `Dashboard ${Date.now()}`
    const created = await page.request.post('/api/v1/admin/polls', {
      data: { title, days: ['2027-03-04'] },
    })
    expect(created.status()).toBe(201)
    const pollId = (await created.json()).id

    await gotoPolls(page)
    await page.getByTestId('nav-dashboard').click()
    await expect(polls()).toBeVisible()
    await expectPollCount(before + 1)

    // A poll with no answers is counted as one waiting for them.
    await expect(page.getByTestId('stat-unanswered')).toContainText(/[1-9]/)

    // And the figures follow a deletion too, which is the half a cached total would get wrong.
    const deleted = await page.request.delete(`/api/v1/admin/polls/${pollId}`)
    expect(deleted.status()).toBe(204)

    await gotoPolls(page)
    await page.getByTestId('nav-dashboard').click()
    await expect(polls()).toBeVisible()
    await expectPollCount(before)
  })

  /**
   * 007 SC-005 - the criterion that records this feature's one accepted cost.
   *
   * Creating a poll gains exactly one action, because its form is no longer permanently open.
   * Every other date-poll task keeps its current count, and the empty state cancels even that
   * one. Nothing else asserted this, and a careless rearrangement is exactly what would break it.
   */
  test('only creating a poll gained a step, and nothing else did', async ({ page }) => {
    await signIn(page)
    await gotoPolls(page)

    // Creating: one action to reveal the form. This is the accepted cost.
    await page.getByTestId('poll-create-toggle').click()
    await expect(page.getByTestId('poll-form')).toBeVisible()

    const title = `Schritte ${Date.now()}`
    const created = await page.request.post('/api/v1/admin/polls', {
      data: { title, days: ['2027-05-05'] },
    })
    expect(created.status()).toBe(201)

    await page.reload()
    const card = page.getByTestId('poll-list-item').filter({ hasText: title })

    // Reading the answers: one action from the list, as before.
    await card.getByTestId('show-results').click()
    await expect(page.getByTestId('poll-answers')).toBeVisible()

    // Exporting and deleting: still one action from the list, not moved onto the answers page
    // (FR-014d) - moving them would have added a step to two tasks that had none.
    await page.getByTestId('back-to-polls').click()
    await expect(card.getByTestId('export-poll')).toBeVisible()
    await expect(card.getByTestId('delete-poll')).toBeVisible()
    await expect(page.getByTestId('poll-answers')).toHaveCount(0)

    await card.getByTestId('delete-poll').click()
    await expect(page.getByTestId('delete-confirm')).toBeVisible()
  })

  /**
   * 007 FR-014l. The exception to the exception: with nothing stored there is nothing for the
   * action to be preferable to, so the empty state offers creating directly.
   */
  test('the empty state offers creating a poll without finding the action first', async ({
    page,
  }) => {
    await signIn(page)

    // Emptied through the API so the assertion is about an empty installation, not about whatever
    // earlier tests happened to leave behind.
    const existing = (await (await page.request.get('/api/v1/admin/polls')).json()) as Array<{
      id: string
    }>
    for (const poll of existing) {
      expect((await page.request.delete(`/api/v1/admin/polls/${poll.id}`)).status()).toBe(204)
    }

    await gotoPolls(page)
    await expect(page.getByTestId('poll-list-empty')).toBeVisible()

    await page.getByTestId('poll-create-empty').click()
    await expect(page.getByTestId('poll-form')).toBeVisible()
  })
})
