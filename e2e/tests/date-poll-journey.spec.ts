import { test, expect, type Page } from '@playwright/test'
import { field, radio } from '../support/fields'
import { ADMIN_PASSWORD, ADMIN_USER } from '../support/credentials'

/** The whole feature end to end: create, answer, read, delete (US1 to US5). */
test.describe('Date poll journey', () => {
  async function signIn(page: Page) {
    await page.goto('/admin')
    await field(page, 'sign-in-user').fill(ADMIN_USER)
    await field(page, 'sign-in-password').fill(ADMIN_PASSWORD)
    await page.getByTestId('sign-in-submit').click()
    await expect(page.getByTestId('poll-form')).toBeVisible()
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

    await page.reload()
    const row = page.getByTestId('poll-list-item').filter({ hasText: title })
    await row.getByTestId('show-results').click()

    await expect(row.getByTestId('result-row')).toHaveCount(3)

    // Two yes and one no on the first day. The second day nobody answered, so its totals are
    // all zero - and they do not sum to three, which is exactly what FR-033 permits.
    const yesRow = row.getByTestId('totals-row').filter({ hasText: 'Ja' }).first()
    const noRow = row.getByTestId('totals-row').filter({ hasText: 'Nein' }).first()
    await expect(yesRow.locator('td').first()).toHaveText('2')
    await expect(noRow.locator('td').first()).toHaveText('1')
    await expect(yesRow.locator('td').nth(1)).toHaveText('0')

    await expect(row.getByTestId('response-count')).toContainText('3')
  })

  test('the operator removes a single answer and the totals follow', async ({ page, browser }) => {
    // FR-037a, FR-037b
    const title = `Einzelloeschung ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2026-11-18'])

    await answer(browser, path, 'Anna', 'yes')
    await answer(browser, path, 'Bernd', 'no')

    await page.reload()
    const row = page.getByTestId('poll-list-item').filter({ hasText: title })
    await row.getByTestId('show-results').click()
    await expect(row.getByTestId('result-row')).toHaveCount(2)

    await row.getByTestId('delete-response').first().click()

    await expect(row.getByTestId('result-row')).toHaveCount(1)
    await expect(row.getByTestId('result-row')).toContainText('Bernd')
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
