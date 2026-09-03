import { test, expect, type Page } from '@playwright/test'
import { field, radio } from '../support/fields'

const USER = process.env.E2E_ADMIN_USER
const PASSWORD = process.env.E2E_ADMIN_PASSWORD

/**
 * FR-047 and Principle I, proven from the outside.
 *
 * The point of these tests is what is *absent*: no account, no session, no email, no step
 * between the link and the form. A test that merely submits an answer would pass even if a
 * login had crept in front of it, so each one checks the absence explicitly.
 */
test.describe('Answering without an account', () => {
  async function createPoll(page: Page, title: string, days: string[]): Promise<string> {
    if (!USER || !PASSWORD) {
      throw new Error('E2E_ADMIN_USER and E2E_ADMIN_PASSWORD must be set - see README.md.')
    }

    await page.goto('/admin')
    await field(page, 'sign-in-user').fill(USER)
    await field(page, 'sign-in-password').fill(PASSWORD)
    await page.getByTestId('sign-in-submit').click()
    await expect(page.getByTestId('poll-form')).toBeVisible()

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

  test('an unknown link shows the same nothing as an expired one', async ({ browser }) => {
    // SC-012 as a person experiences it.
    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()

    await strangerPage.goto('/u/aaaaaaaaaaaaaaaaaaaaaa')
    await expect(strangerPage.getByTestId('poll-not-found')).toBeVisible()
    await expect(strangerPage.getByTestId('answer-form')).toHaveCount(0)

    await stranger.close()
  })
})
