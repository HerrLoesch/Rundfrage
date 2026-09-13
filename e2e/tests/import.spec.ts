import { test, expect } from '@playwright/test'
import { field } from '../support/fields'
import { revealImportPanel, revealPollForm, signInToPolls } from '../support/admin'

/**
 * US1 from the outside: an operator exports a poll, reads it back in, and shares the link the
 * import produced — which is a different link, and the interface has to say so.
 */
test.describe('Import (US1)', () => {
  /**
   * 007: signing in lands on the dashboard, so reaching a control is now "sign in, then go to the
   * area it lives in". The shared helper carries that so a future rearrangement is one edit.
   */
  /**
   * Signs in and lands in the poll area, with neither form revealed.
   *
   * Import lives with the polls because it *makes* a poll - deliberately far from the restore,
   * which replaces every poll and sits in settings (007 FR-019, FR-025). Creating and importing
   * are separate actions and only one form may be open at a time (FR-014i), so a test that does
   * both reveals them in turn.
   */
  async function signIn(page: import('@playwright/test').Page) {
    await signInToPolls(page)
  }

  test('an exported poll can be read back in, and a participant answers through the new link', async ({
    page,
  }) => {
    await signIn(page)
    await revealPollForm(page)

    await field(page, 'poll-title').fill('Wiedereingelesen')
    await field(page, 'poll-day-input').fill('2027-11-05')
    await page.getByTestId('poll-add-day').click()
    await page.getByTestId('poll-submit').click()

    const originalLink = await page.getByTestId('poll-share-link').first().innerText()

    // Export through the API, the same bytes the operator would download.
    //
    // page.request, not the `request` fixture: the fixture is its own context with its own cookie
    // jar, so a session established in the browser does not reach it. Written the other way round
    // first, and this listing came back as {"code":"unauthorized"} rather than an array.
    const listing = await page.request.get('/api/v1/admin/polls')
    const polls = (await listing.json()) as Array<{ id: string; title: string }>
    const created = polls.find((p) => p.title === 'Wiedereingelesen')!
    const exported = await page.request.get(`/api/v1/admin/polls/${created.id}/export`)
    const document = await exported.text()

    await revealImportPanel(page)
    await page.getByTestId('import-file').locator('input[type=file]').setInputFiles({
      name: 'wiedereingelesen.json',
      mimeType: 'application/json',
      buffer: Buffer.from(document, 'utf8'),
    })
    await page.getByTestId('import-submit').click()

    await expect(page.getByTestId('import-summary')).toBeVisible()

    const importedLink = await page.getByTestId('import-link').innerText()

    // FR-011: a new link, and the interface says the old one does not reach it.
    expect(importedLink).not.toBe(originalLink)
    await expect(page.getByTestId('import-link-hint')).toBeVisible()

    // The imported poll is a working poll.
    await page.goto(new URL(importedLink).pathname)
    await expect(page.getByTestId('poll-view-title')).toContainText('Wiedereingelesen')
  })

  test('a file that is not an export is refused, and nothing is created', async ({ page }) => {
    await signIn(page)

    // Also page.request. With the unauthenticated fixture this read an object rather than an
    // array, `.length` was undefined at both ends, and the assertion below compared undefined to
    // undefined - passing while proving nothing.
    const before = ((await (await page.request.get('/api/v1/admin/polls')).json()) as unknown[]).length

    await revealImportPanel(page)
    await page.getByTestId('import-file').locator('input[type=file]').setInputFiles({
      name: 'nonsense.json',
      mimeType: 'application/json',
      buffer: Buffer.from('this is not an export', 'utf8'),
    })
    await page.getByTestId('import-submit').click()

    await expect(page.getByTestId('import-error')).toBeVisible()
    await expect(page.getByTestId('import-summary')).toHaveCount(0)

    const after = ((await (await page.request.get('/api/v1/admin/polls')).json()) as unknown[]).length
    expect(after).toBe(before)
    expect(before).toBeGreaterThanOrEqual(0)
  })
})
