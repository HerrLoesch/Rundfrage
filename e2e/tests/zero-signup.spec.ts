import { test, expect, type Page } from '@playwright/test'
import { field, radio } from '../support/fields'
import { revealPollForm, revealWishListForm, signInToPolls, signInToWishLists } from '../support/admin'

/**
 * FR-047 and Principle I, proven from the outside.
 *
 * The point of these tests is what is *absent*: no account, no session, no email, no step
 * between the link and the form. A test that merely submits an answer would pass even if a
 * login had crept in front of it, so each one checks the absence explicitly.
 */
test.describe('Answering without an account', () => {
  async function createPoll(page: Page, title: string, days: string[]): Promise<string> {
    await signInToPolls(page)
    await revealPollForm(page)

    await field(page, 'poll-title').fill(title)
    for (const day of days) {
      await field(page, 'poll-day-input').fill(day)
      await page.getByTestId('poll-add-day').click()
    }
    await page.getByTestId('poll-submit').click()

    const link = await page.getByTestId('poll-share-link').textContent()
    return new URL(link!.trim()).pathname
  }

  test('a stranger opens the link and sees the form immediately', async ({ browser, page }) => {
    const path = await createPoll(page, `Ohne Konto ${Date.now()}`, ['2026-11-18', '2026-11-20'])

    // A brand-new context: no cookies, no storage, nothing carried over from the operator.
    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()
    await strangerPage.goto(path)

    // FR-019 and FR-021: title, days and form on the first load, with nothing in between.
    await expect(strangerPage.getByTestId('poll-view-title')).toBeVisible()
    await expect(strangerPage.getByTestId('answer-form')).toBeVisible()
    await expect(strangerPage.getByTestId('sign-in-form')).toHaveCount(0)

    // FR-003: and it carried no credential of any kind.
    expect(await stranger.cookies()).toEqual([])

    await stranger.close()
  })

  test('the visibility notice appears before the name is entered', async ({ browser, page }) => {
    // FR-036a
    const path = await createPoll(page, `Hinweis ${Date.now()}`, ['2026-11-18'])

    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()
    await strangerPage.goto(path)

    await expect(strangerPage.getByTestId('visibility-notice')).toBeVisible()
    await expect(field(strangerPage, 'participant-name')).toBeEmpty()

    await stranger.close()
  })

  test('a complete answer can be submitted with the keyboard alone', async ({ browser, page }) => {
    // SC-025 and FR-050: no pointing device is used anywhere in this test.
    const path = await createPoll(page, `Tastatur ${Date.now()}`, ['2026-11-18', '2026-11-20'])

    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()
    await strangerPage.goto(path)

    await field(strangerPage, 'participant-name').focus()
    await strangerPage.keyboard.type('Nur Tastatur')

    // Tab into each day group and choose with the arrow keys, as native radios allow.
    for (let day = 0; day < 2; day++) {
      await strangerPage.keyboard.press('Tab')
      await strangerPage.keyboard.press('ArrowRight')
    }

    await strangerPage.keyboard.press('Tab')
    await strangerPage.keyboard.press('Enter')

    await expect(strangerPage.getByTestId('submitted-confirmation')).toBeVisible()

    await stranger.close()
  })

  test('answering, then reading the grid, then revising - all without an account', async ({
    browser,
    page,
  }) => {
    const path = await createPoll(page, `Durchstich ${Date.now()}`, ['2026-11-18', '2026-11-20'])

    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()
    await strangerPage.goto(path)

    await field(strangerPage, 'participant-name').fill('Anna')
    const days = strangerPage.getByTestId('day-choice')
    await radio(days.nth(0), 'choice-yes').check()
    await radio(days.nth(1), 'choice-no').check()
    await strangerPage.getByTestId('answer-submit').click()

    await expect(strangerPage.getByTestId('submitted-confirmation')).toBeVisible()

    // FR-026: the personal link is shown, because there is no account to look the answer up with.
    const personalLink = await strangerPage.getByTestId('share-url').textContent()
    expect(personalLink).toContain('/a/')

    // The answer is in the grid, with the name as a label.
    await expect(strangerPage.getByTestId('result-row')).toHaveCount(1)
    await expect(strangerPage.getByTestId('result-row').first()).toContainText('Anna')

    // FR-028 to FR-030: revise through the personal link, still with no account.
    const revisionContext = await browser.newContext()
    const revisionPage = await revisionContext.newPage()
    await revisionPage.goto(new URL(personalLink!.trim()).pathname)

    await expect(field(revisionPage, 'participant-name')).toHaveValue('Anna')
    await radio(revisionPage.getByTestId('day-choice').nth(0), 'choice-no').check()
    await revisionPage.getByTestId('answer-submit').click()

    await expect(revisionPage.getByTestId('revised-confirmation')).toBeVisible()
    // Updated in place: still one response, not two.
    await expect(revisionPage.getByTestId('result-row')).toHaveCount(1)

    await stranger.close()
    await revisionContext.close()
  })

  test('pressing send twice does not record the answer twice', async ({ browser, page }) => {
    // Reported: sending the same answer again recorded it a second time. The form stayed in
    // "submit" mode with everything still filled in, so the second press created a new
    // response. It now revises, using the token the first submission returned.
    const path = await createPoll(page, `Doppelt ${Date.now()}`, ['2026-11-18'])

    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()
    await strangerPage.goto(path)

    await field(strangerPage, 'participant-name').fill('Anna')
    await radio(strangerPage.getByTestId('day-choice').nth(0), 'choice-yes').check()
    await strangerPage.getByTestId('answer-submit').click()
    await expect(strangerPage.getByTestId('submitted-confirmation')).toBeVisible()
    await expect(strangerPage.getByTestId('result-row')).toHaveCount(1)

    // Unchanged, pressed again.
    await strangerPage.getByTestId('answer-submit').click()
    await expect(strangerPage.getByTestId('revised-confirmation')).toBeVisible()
    await expect(strangerPage.getByTestId('result-row')).toHaveCount(1)

    // And a genuine change still lands, in place.
    await radio(strangerPage.getByTestId('day-choice').nth(0), 'choice-no').check()
    await strangerPage.getByTestId('answer-submit').click()
    await expect(strangerPage.getByTestId('result-row')).toHaveCount(1)
    await expect(strangerPage.getByTestId('result-cell').first()).toHaveAttribute('data-state', 'no')

    await stranger.close()
  })

  test('an unknown link shows the same nothing as an expired one', async ({ browser }) => {
    // SC-012 as a person experiences it.
    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()

    await strangerPage.goto('/u/aaaaaaaaaaaaaaaaaaaaaa')
    await expect(strangerPage.getByTestId('poll-not-found')).toBeVisible()
    await expect(strangerPage.getByTestId('answer-form')).toHaveCount(0)

    await stranger.close()
  })

  /**
   * 007 FR-009 and SC-010. The admin area grew a navigation bar, and the one thing it must never
   * do is offer itself to a participant. Asserted as an absence, because that is the whole
   * requirement: a link into an area they cannot enter is a step between the link and the form.
   */
  test('a participant is offered no navigation and no route into the admin area', async ({
    page,
  }) => {
    const path = await createPoll(page, `Ohne Navigation ${Date.now()}`, ['2026-12-02'])

    const context = await page.context().browser()!.newContext()
    const participant = await context.newPage()
    await participant.goto(path)
    await expect(participant.getByTestId('answer-form')).toBeVisible()

    // None of the shell, and nothing that leads to it.
    await expect(participant.getByTestId('admin-shell')).toHaveCount(0)
    await expect(participant.getByTestId('admin-nav')).toHaveCount(0)
    await expect(participant.getByTestId('sign-out')).toHaveCount(0)
    await expect(participant.getByTestId('nav-dashboard')).toHaveCount(0)

    const adminLinks = await participant.locator('a[href*="/admin"]').count()
    expect(adminLinks).toBe(0)

    // And still nothing between the link and the form: the answer form is on the landing page.
    await expect(participant.getByTestId('answer-submit')).toBeVisible()

    await context.close()
  })
  test('a wish list is claimed without an account, and shows no way into the admin area', async ({
    browser,
    page,
  }) => {
    // 008 FR-014 and FR-026. The same absences the poll link is held to: no account, no step
    // before the form, and no navigation towards an area a participant cannot enter.
    await signInToWishLists(page)
    await revealWishListForm(page)
    await field(page, 'wish-form-title').fill(`Ohne Konto ${Date.now()}`)
    await field(page, 'wish-form-target-date').fill('2099-07-18')
    await field(page, 'wish-form-item-name-0').fill('Kuchen')
    await field(page, 'wish-form-item-count-0').fill('2')
    await page.getByTestId('wish-form-submit').click()

    const link = await page.getByTestId('wish-share-url').textContent()
    const path = new URL(link!.trim()).pathname

    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()
    await strangerPage.goto(path)

    // The form is on the page that loaded - zero steps between the link and it.
    await expect(strangerPage.getByTestId('wish-claim-form')).toBeVisible()
    await expect(strangerPage.getByTestId('admin-nav')).toHaveCount(0)
    await expect(strangerPage.locator('a[href*="/admin"]')).toHaveCount(0)

    // And claiming needs nothing but a name.
    await strangerPage.locator('[data-testid^="wish-choose-"]').first().locator('input').check()
    await field(strangerPage, 'wish-name').fill('Anna')
    await strangerPage.getByTestId('wish-submit').click()
    await expect(strangerPage.getByTestId('wish-submitted')).toBeVisible()

    await stranger.close()
  })

})
