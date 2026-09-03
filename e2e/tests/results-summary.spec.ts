import { test, expect, type Page } from '@playwright/test'
import { field, radio } from '../support/fields'
import { ADMIN_PASSWORD, ADMIN_USER } from '../support/credentials'

/**
 * The half of feature 004 that only a browser can judge.
 *
 * Position after a sideways scroll, focus order, what a screen reader is told, a second tab —
 * jsdom computes none of it. Feature 003 shipped a logo four pixels off centre with every
 * component test green; everything positional here is checked where layout actually exists.
 */
test.describe('Results summary and followable addresses', () => {
  async function signIn(page: Page) {
    await page.goto('/admin')
    await field(page, 'sign-in-user').fill(ADMIN_USER)
    await field(page, 'sign-in-password').fill(ADMIN_PASSWORD)
    await page.getByTestId('sign-in-submit').click()
    await expect(page.getByTestId('poll-form')).toBeVisible()
  }

  /** Creates a poll through the API, using the session the page already holds. */
  async function createPoll(page: Page, title: string, days: string[]): Promise<string> {
    const created = await page.request.post('/api/v1/admin/polls', { data: { title, days } })
    expect(created.status()).toBe(201)
    return `/u/${(await created.json()).participantToken}`
  }

  async function answer(page: Page, path: string, name: string, choice: string) {
    const context = await page.context().browser()!.newContext()
    const tab = await context.newPage()
    await tab.goto(path)
    await field(tab, 'participant-name').fill(name)
    await radio(tab.getByTestId('day-choice').nth(0), `choice-${choice}`).check()
    await tab.getByTestId('answer-submit').click()
    await expect(tab.getByTestId('submitted-confirmation')).toBeVisible()
    await context.close()
  }

  test('the summary folds and unfolds with the keyboard alone', async ({ page }) => {
    // FR-005, SC-004. Reached by tabbing, not by clicking a coordinate.
    const title = `Tastatur ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2026-11-18'])
    await answer(page, path, 'Anna', 'yes')

    await page.goto(path)
    const toggle = page.getByTestId('summary-toggle')
    await expect(toggle).toHaveAttribute('aria-expanded', 'false')

    await toggle.focus()
    await expect(toggle).toBeFocused()
    await page.keyboard.press('Enter')
    await expect(toggle).toHaveAttribute('aria-expanded', 'true')
    await expect(page.getByTestId('summary-row')).toHaveCount(3)

    await page.keyboard.press('Enter')
    await expect(toggle).toHaveAttribute('aria-expanded', 'false')
    await expect(page.getByTestId('summary-row')).toHaveCount(0)
  })

  test('a folded summary is absent from what a screen reader reads', async ({ page }) => {
    // FR-006. Measured against the browser's own accessibility tree, not against visibility:
    // an element can be invisible and still announced, and that is the failure worth catching.
    const title = `Vorlesen ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2026-11-18'])
    await answer(page, path, 'Anna', 'yes')

    await page.goto(path)
    const grid = page.getByTestId('result-grid')

    const folded = await grid.ariaSnapshot()
    expect(folded).not.toContain('Vielleicht')

    await page.getByTestId('summary-toggle').click()
    await expect(page.getByTestId('summary-row')).toHaveCount(3)

    const unfolded = await grid.ariaSnapshot()
    expect(unfolded).toContain('Vielleicht')
  })

  test('each summary stays over its own day when a hundred days scroll sideways', async ({
    page,
  }) => {
    // FR-013 and SC-005 - the requirement the whole placement decision rests on. Measured
    // against a synthetic table while planning; this is where it has to be true of the real one.
    const title = `Hundert Tage ${Date.now()}`
    const days = Array.from({ length: 100 }, (_, i) => {
      const d = new Date(Date.UTC(2027, 0, 1 + i))
      return d.toISOString().slice(0, 10)
    })

    await signIn(page)
    const path = await createPoll(page, title, days)
    await answer(page, path, 'Anna', 'yes')

    await page.goto(path)
    await page.getByTestId('summary-toggle').click()
    await expect(page.getByTestId('summary-row')).toHaveCount(3)

    const offsets = async () =>
      page.evaluate(() => {
        const summary = document.querySelector('[data-testid="summary-row"]')!
        const rows = [...document.querySelectorAll('thead tr')]
        const dateRow = rows[rows.length - 1]
        const left = (row: Element, i: number) =>
          Math.round(row.children[i].getBoundingClientRect().left)
        return [1, 50, 100].map((i) => ({ i, summary: left(summary, i), date: left(dateRow, i) }))
      })

    for (const { i, summary, date } of await offsets()) {
      expect(summary, `column ${i} before scrolling`).toBe(date)
    }

    await page.evaluate(() => {
      document.querySelector('.scroller')!.scrollLeft = 2400
    })

    for (const { i, summary, date } of await offsets()) {
      expect(summary, `column ${i} after scrolling`).toBe(date)
    }

    // SC-005: the grid scrolls, the page does not.
    const bodyScrolls = await page.evaluate(
      () => document.documentElement.scrollWidth > document.documentElement.clientWidth,
    )
    expect(bodyScrolls).toBe(false)
  })

  test('a participant with no account finds the summary folded too', async ({ page, browser }) => {
    // FR-003, FR-014, SC-001. One behaviour, not one per audience.
    const title = `Ohne Konto ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2026-11-18'])
    await answer(page, path, 'Anna', 'yes')

    const stranger = await browser.newContext()
    const tab = await stranger.newPage()
    await tab.goto(path)

    await expect(tab.getByTestId('summary-row')).toHaveCount(0)
    await expect(tab.getByTestId('summary-toggle')).toHaveAttribute('aria-expanded', 'false')

    await tab.getByTestId('summary-toggle').click()
    await expect(tab.getByTestId('summary-row')).toHaveCount(3)

    await stranger.close()
  })

  test('the poll address opens in a second tab and leaves the admin area standing', async ({
    page,
    context,
  }) => {
    // FR-016, FR-018, SC-006. The destination is exactly the address that was already on screen:
    // a clickable link is a convenience, not a new capability.
    const title = `Zweiter Tab ${Date.now()}`
    await signIn(page)
    await createPoll(page, title, ['2026-11-18'])

    await page.goto('/admin')
    const card = page.getByTestId('poll-list-item').filter({ hasText: title })
    const link = card.getByTestId('poll-list-link')
    const address = (await link.textContent())!.trim()

    const [opened] = await Promise.all([context.waitForEvent('page'), link.click()])
    await opened.waitForLoadState()

    expect(opened.url()).toBe(await link.getAttribute('href'))
    await expect(opened.getByText(title)).toBeVisible()

    // The admin tab is untouched: still signed in, still showing the list.
    await expect(page.getByTestId('poll-form')).toBeVisible()
    await expect(card).toBeVisible()
    await opened.close()

    // FR-017: the link's text is still the bare address - the new-tab note describes the link
    // rather than sitting inside it, which is what keeps this true.
    expect(address).toBe(await link.getAttribute('href'))
    expect(await link.locator('.d-sr-only').count()).toBe(0)
  })

  test('the personal address is a link too, and the copy control still works', async ({
    page,
    browser,
  }) => {
    // FR-015a and SC-007. With no account this address is the only way back to one's own
    // answer, which is why it was included even though the request named the admin area.
    const title = `Persoenlich ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2026-11-18'])

    const stranger = await browser.newContext()
    const tab = await stranger.newPage()
    await tab.goto(path)
    await field(tab, 'participant-name').fill('Anna')
    await radio(tab.getByTestId('day-choice').nth(0), 'choice-yes').check()
    await tab.getByTestId('answer-submit').click()
    await expect(tab.getByTestId('submitted-confirmation')).toBeVisible()

    const personal = tab.getByTestId('share-url')
    expect(await personal.evaluate((el) => el.tagName)).toBe('A')
    expect(await personal.getAttribute('href')).toContain('/a/')
    expect(await personal.getAttribute('target')).toBe('_blank')
    expect((await personal.textContent())!.trim()).toBe(await personal.getAttribute('href'))

    await expect(tab.getByTestId('share-copy')).toBeVisible()
    await stranger.close()
  })
})
