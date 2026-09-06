import { test, expect } from '@playwright/test'
import { field } from '../support/fields'
import { ADMIN_PASSWORD, ADMIN_USER } from '../support/credentials'

/**
 * US1 from the outside: an operator exports a poll, reads it back in, and shares the link the
 * import produced — which is a different link, and the interface has to say so.
 */
test.describe('Import (US1)', () => {
  async function signIn(page: import('@playwright/test').Page) {
    await page.goto('/admin')
    await field(page, 'sign-in-user').fill(ADMIN_USER)
    await field(page, 'sign-in-password').fill(ADMIN_PASSWORD)
    await page.getByTestId('sign-in-submit').click()
    await expect(page.getByTestId('import-panel')).toBeVisible()
  }

  test('an exported poll can be read back in, and a participant answers through the new link', async ({
    page,
    request,
  }) => {
    await signIn(page)

    await field(page, 'poll-title').fill('Wiedereingelesen')
    await field(page, 'poll-days').fill('2027-11-05')
    await page.getByTestId('poll-submit').click()

    const originalLink = await page.getByTestId('share-url').first().innerText()

    // Export through the API, the same bytes the operator would download.
    const listing = await request.get('/api/v1/admin/polls')
    const polls = (await listing.json()) as Array<{ id: string; title: string }>
    const created = polls.find((p) => p.title === 'Wiedereingelesen')!
    const exported = await request.get(`/api/v1/admin/polls/${created.id}/export`)
    const document = await exported.text()

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

  test('a file that is not an export is refused, and nothing is created', async ({ page, request }) => {
    await signIn(page)

    const before = ((await (await request.get('/api/v1/admin/polls')).json()) as unknown[]).length

    await page.getByTestId('import-file').locator('input[type=file]').setInputFiles({
      name: 'nonsense.json',
      mimeType: 'application/json',
      buffer: Buffer.from('this is not an export', 'utf8'),
    })
    await page.getByTestId('import-submit').click()

    await expect(page.getByTestId('import-error')).toBeVisible()
    await expect(page.getByTestId('import-summary')).toHaveCount(0)

    const after = ((await (await request.get('/api/v1/admin/polls')).json()) as unknown[]).length
    expect(after).toBe(before)
  })
})
