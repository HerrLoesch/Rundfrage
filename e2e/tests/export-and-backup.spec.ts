import { test, expect } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { field, radio } from '../support/fields'
import { ADMIN_USER, ADMIN_PASSWORD } from '../support/credentials'

/**
 * US2 and the backup half of US1, through the browser that actually downloads them.
 *
 * A backend test can prove the bytes are right. Only this can prove the person gets a file:
 * the link carries the session cookie, the browser honours Content-Disposition, and what lands
 * on disk parses.
 */
test.describe('Export and backup', () => {
  async function signIn(page: import('@playwright/test').Page) {
    await page.goto('/admin')
    await field(page, 'sign-in-user').fill(ADMIN_USER)
    await field(page, 'sign-in-password').fill(ADMIN_PASSWORD)
    await page.getByTestId('sign-in-submit').click()
    await expect(page.getByTestId('poll-form')).toBeVisible()
  }

  test('a poll with two answers downloads as JSON that parses', async ({ page, context }) => {
    const title = `Export ${Date.now()}`

    await signIn(page)
    await field(page, 'poll-title').fill(title)
    await page.getByTestId('poll-add-day').click()
    await page.getByTestId('poll-submit').click()

    const link = (await page.getByTestId('poll-share-link').textContent())!.trim()

    // Two participants answer, each in their own context: no session, no account (Principle I).
    for (const [name, availability] of [
      ['Anna', 'yes'],
      ['Bert', 'no'],
    ] as const) {
      const participant = await context.browser()!.newContext()
      const tab = await participant.newPage()
      await tab.goto(new URL(link).pathname)
      await field(tab, 'participant-name').fill(name)
      await radio(tab.getByTestId('day-choice').nth(0), `choice-${availability}`).check()
      await tab.getByTestId('answer-submit').click()
      await expect(tab.getByTestId('submitted-confirmation')).toBeVisible()
      await participant.close()
    }

    await page.goto('/admin')
    const card = page.getByTestId('poll-list-item').filter({ hasText: title })

    const download = await Promise.all([
      page.waitForEvent('download'),
      card.getByTestId('export-poll').click(),
    ]).then(([d]) => d)

    // The name identifies the poll and the moment, so two exports can share a folder (FR-021a).
    expect(download.suggestedFilename()).toMatch(/^export-\d+-\d{4}-\d{2}-\d{2}T\d{6}Z\.json$/)

    const file = await download.path()
    const document = JSON.parse(readFileSync(file, 'utf8'))

    expect(document.formatVersion).toBe(1)
    expect(document.poll.title).toBe(title)
    expect(document.responses.map((r: { displayName: string }) => r.displayName).sort()).toEqual([
      'Anna',
      'Bert',
    ])

    // FR-015: whoever receives this file must not receive anyone's capability with it.
    const raw = readFileSync(file, 'utf8')
    expect(raw).not.toContain(link.split('/u/')[1])
  })

  test('the whole storage downloads as one backup file', async ({ page }) => {
    // FR-003. The button is beside the list rather than on a card, because a backup is the
    // storage and not one poll.
    await signIn(page)

    const download = await Promise.all([
      page.waitForEvent('download'),
      page.getByTestId('download-backup').click(),
    ]).then(([d]) => d)

    expect(download.suggestedFilename()).toMatch(/^rundfrage-\d{4}-\d{2}-\d{2}T\d{6}Z\.db$/)

    const bytes = readFileSync(await download.path())
    // Every SQLite file begins with this. A zero-length or HTML error page would not.
    expect(bytes.subarray(0, 15).toString('utf8')).toBe('SQLite format 3')
  })
})
