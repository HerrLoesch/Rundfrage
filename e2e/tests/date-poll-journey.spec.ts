import { test, expect, type Page } from '@playwright/test'
import { field, radio } from '../support/fields'
import { revealPollForm, signInToPolls } from '../support/admin'

/** The whole feature end to end: create, answer, read, delete (US1 to US5). */
test.describe('Date poll journey', () => {
  /**
   * 007: signing in lands on the dashboard, so reaching a control is now "sign in, then go to the
   * area it lives in". The shared helper carries that so a future rearrangement is one edit.
   */
  async function signIn(page: Page) {
    await signInToPolls(page)
    await revealPollForm(page)
  }

  async function createPoll(page: Page, title: string, days: string[]): Promise<string> {
    await field(page, 'poll-title').fill(title)
    for (const day of days) {
      await field(page, 'poll-day-input').fill(day)
      await page.getByTestId('poll-add-day').click()
    }
    await page.getByTestId('poll-submit').click()
    const link = await page.getByTestId('poll-share-link').textContent()
    return new URL(link!.trim()).pathname
  }

  async function answer(browser: import('@playwright/test').Browser, path: string, name: string, choice: string) {
    const context = await browser.newContext()
    const page = await context.newPage()
    await page.goto(path)
    await field(page, 'participant-name').fill(name)
    await radio(page.getByTestId('day-choice').nth(0), `choice-${choice}`).check()
    await page.getByTestId('answer-submit').click()
    await expect(page.getByTestId('submitted-confirmation')).toBeVisible()
    await context.close()
  }

  test('the operator sees the answers and their totals', async ({ page, browser }) => {
    // US3
    const title = `Auswertung ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2026-11-18', '2026-11-20'])

    await answer(browser, path, 'Anna', 'yes')
    await answer(browser, path, 'Bernd', 'yes')
    await answer(browser, path, 'Christa', 'no')

    // 007 FR-014a: the answers are a destination now, not an expansion of the card.
    await page.reload()
    await page.getByTestId('poll-list-item').filter({ hasText: title })
      .getByTestId('show-results').click()

    const answers = page.getByTestId('poll-answers')
    await expect(answers).toBeVisible()
    await expect(page).toHaveURL(/\/admin\/terminfindungen\/[0-9a-f-]+$/)
    await expect(answers.getByTestId('result-row')).toHaveCount(3)

    // The summary starts folded (004 FR-003), so it has to be unfolded first - because a person
    // does too. Reaching past the control would test a journey nobody takes.
    await answers.getByTestId('summary-toggle').click()

    // Two yes and one no on the first day. The second day nobody answered, so its totals are
    // all zero - and they do not sum to three, which is exactly what FR-033 permits.
    const yesRow = answers.getByTestId('summary-row').filter({ hasText: 'Ja' }).first()
    const noRow = answers.getByTestId('summary-row').filter({ hasText: 'Nein' }).first()
    await expect(yesRow.locator('td').first()).toHaveText('2')
    await expect(noRow.locator('td').first()).toHaveText('1')
    await expect(yesRow.locator('td').nth(1)).toHaveText('0')

    await expect(answers.getByTestId('response-count')).toContainText('3')

    // 007 SC-006: the address of one poll's answers reloads to the same answers.
    await page.reload()
    await expect(page.getByTestId('poll-answers').getByTestId('result-row')).toHaveCount(3)
  })

  test('the operator removes a single answer and the totals follow', async ({ page, browser }) => {
    // FR-037a, FR-037b
    const title = `Einzelloeschung ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2026-11-18'])

    await answer(browser, path, 'Anna', 'yes')
    await answer(browser, path, 'Bernd', 'no')

    await page.reload()
    await page.getByTestId('poll-list-item').filter({ hasText: title })
      .getByTestId('show-results').click()

    const answers = page.getByTestId('poll-answers')
    await expect(answers.getByTestId('result-row')).toHaveCount(2)

    await answers.getByTestId('delete-response').first().click()

    await expect(answers.getByTestId('result-row')).toHaveCount(1)
    await expect(answers.getByTestId('result-row')).toContainText('Bernd')

    // 007 FR-014f: deleting an answer leaves the operator with the poll they were reading.
    await expect(page).toHaveURL(/\/admin\/terminfindungen\/[0-9a-f-]+$/)
  })

  test('deleting a poll states how many answers it destroys, then kills both links', async ({
    page,
    browser,
  }) => {
    // FR-038 and FR-040
    const title = `Loeschung ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2026-11-18'])

    await answer(browser, path, 'Anna', 'yes')
    await answer(browser, path, 'Bernd', 'maybe')

    await page.reload()
    const row = page.getByTestId('poll-list-item').filter({ hasText: title })
    await row.getByTestId('delete-poll').click()

    // FR-038: the number is stated before anything is destroyed.
    await expect(page.getByTestId('delete-confirm-body')).toContainText('2')
    await expect(page.getByTestId('delete-confirm-body')).toContainText(title)

    await page.getByTestId('delete-confirm-button').click()

    await expect(page.getByTestId('poll-list-item').filter({ hasText: title })).toHaveCount(0)

    // FR-040: the participant link is gone too, with the same neutral nothing.
    const stranger = await browser.newContext()
    const strangerPage = await stranger.newPage()
    await strangerPage.goto(path)
    await expect(strangerPage.getByTestId('poll-not-found')).toBeVisible()
    await stranger.close()
  })

  test('cancelling the confirmation destroys nothing', async ({ page }) => {
    const title = `Abbruch ${Date.now()}`
    await signIn(page)
    await createPoll(page, title, ['2026-11-18'])

    await page.reload()
    const row = page.getByTestId('poll-list-item').filter({ hasText: title })
    await row.getByTestId('delete-poll').click()
    await page.getByTestId('delete-cancel').click()

    await expect(page.getByTestId('delete-confirm')).toHaveCount(0)
    await expect(page.getByTestId('poll-list-item').filter({ hasText: title })).toHaveCount(1)
  })
})
