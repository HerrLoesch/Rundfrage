import { test, expect, type Page } from '@playwright/test'
import { field, radio } from '../support/fields'
import { signIn as sharedSignIn } from '../support/admin'

/**
 * The half of feature 004 that only a browser can judge.
 *
 * Position after a sideways scroll, focus order, what a screen reader is told, a second tab —
 * jsdom computes none of it. Feature 003 shipped a logo four pixels off centre with every
 * component test green; everything positional here is checked where layout actually exists.
 */
test.describe('Results summary and followable addresses', () => {
  /**
   * 007: signing in lands on the dashboard, so reaching a control is now "sign in, then go to the
   * area it lives in". The shared helper carries that so a future rearrangement is one edit.
   */
  async function signIn(page: Page) {
    // This suite creates its polls through the API and then reads them on the participant page,
    // so the dashboard is far enough: it needs the session, not the poll area.
    await sharedSignIn(page)
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

    await page.goto('/admin/terminfindungen')
    const card = page.getByTestId('poll-list-item').filter({ hasText: title })
    const link = card.getByTestId('poll-list-link')
    const address = (await link.textContent())!.trim()

    const [opened] = await Promise.all([context.waitForEvent('page'), link.click()])
    await opened.waitForLoadState()

    expect(opened.url()).toBe(await link.getAttribute('href'))
    await expect(opened.getByText(title)).toBeVisible()

    // The admin tab is untouched: still signed in, still showing the list.
    await expect(page.getByTestId('poll-create-toggle')).toBeVisible()
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

  test('the best day is marked, and the mark explains itself on focus', async ({ page }) => {
    // 006 FR-001, FR-007, SC-004a. The focus half is why this is an end-to-end test: jsdom has no
    // focus ring and no tooltip, so "reachable without a pointer" can only be judged in a browser.
    const title = `Bester Tag ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2027-04-12', '2027-04-13'])

    // One yes on the first day and nothing on the second, so the first day leads.
    await answer(page, path, 'Anna', 'yes')

    await page.goto(path)
    await page.getByTestId('summary-toggle').click()

    const marks = page.getByTestId('best-day')
    await expect(marks).toHaveCount(1)
    await expect(marks.first()).toHaveAttribute('aria-label', 'bester Tag')

    // Reachable by keyboard, which is the half a pointer-only design would fail.
    await marks.first().focus()
    await expect(marks.first()).toBeFocused()
  })

  test('the creator sees the same day marked as the participant does', async ({ page }) => {
    // 006 FR-004, SC-003. One component serves both views; this is the check that nobody has
    // branched on the viewer.
    const title = `Beide Ansichten ${Date.now()}`
    await signIn(page)
    const path = await createPoll(page, title, ['2027-04-20', '2027-04-21'])
    await answer(page, path, 'Anna', 'yes')

    await page.goto(path)
    await page.getByTestId('summary-toggle').click()
    const participantMarks = await page.getByTestId('best-day').count()

    // 007 FR-014a: the creator's view of the answers is its own address now.
    await page.goto('/admin/terminfindungen')
    await page
      .getByTestId('poll-list-item')
      .filter({ hasText: title })
      .getByTestId('show-results')
      .click()
    await expect(page.getByTestId('poll-answers')).toBeVisible()
    await page.getByTestId('summary-toggle').click()

    await expect(page.getByTestId('best-day')).toHaveCount(participantMarks)
    expect(participantMarks).toBe(1)
  })

  /**
   * The paging control, end to end.
   *
   * Server-side paging at fifty has been the design since 002 (research R-7) and 004's UI
   * contract lists "the paging control and its page size" among what it leaves unchanged - but
   * nothing ever rendered one, so a poll at the 1000-response limit showed fifty answers and
   * offered no way to the rest. Found while reviewing feature 007, which gave the grid an address
   * of its own and made the gap obvious.
   */
  test('a grid with more answers than fit on one page can be paged through', async ({ page }) => {
    const title = `Blaettern ${Date.now()}`
    await signIn(page)

    const created = await page.request.post('/api/v1/admin/polls', {
      data: { title, days: ['2027-08-01'] },
    })
    expect(created.status()).toBe(201)
    const { id, participantToken } = await created.json()

    // Seeded through the API rather than the browser: 51 real submissions would take minutes and
    // prove nothing this test is about.
    const dayId = (await (await page.request.get(`/api/v1/polls/${participantToken}`)).json())
      .days[0].id
    for (let i = 0; i < 51; i++) {
      const submitted = await page.request.post(`/api/v1/polls/${participantToken}/responses`, {
        data: { displayName: `Person ${i}`, answers: [{ dayId, availability: 'yes' }] },
      })
      expect(submitted.status()).toBe(201)
    }

    await page.goto(`/admin/terminfindungen/${id}`)
    await expect(page.getByTestId('result-row')).toHaveCount(50)

    const paging = page.getByTestId('results-paging')
    await expect(paging).toBeVisible()
    await expect(page.getByTestId('results-previous')).toBeDisabled()

    const firstPageNames = await page.getByTestId('result-row').allInnerTexts()

    await page.getByTestId('results-next').click()

    // The 51st answer, which was unreachable before this control existed.
    await expect(page.getByTestId('result-row')).toHaveCount(1)
    await expect(page.getByTestId('results-next')).toBeDisabled()

    const secondPageNames = await page.getByTestId('result-row').allInnerTexts()
    expect(firstPageNames).not.toContain(secondPageNames[0])

    await page.getByTestId('results-previous').click()
    await expect(page.getByTestId('result-row')).toHaveCount(50)
  })

  test('a participant can page through the grid too, without losing a half-filled answer', async ({
    page,
  }) => {
    const title = `Blaettern Teilnehmer ${Date.now()}`
    await signIn(page)

    const created = await page.request.post('/api/v1/admin/polls', {
      data: { title, days: ['2027-09-01'] },
    })
    const { participantToken } = await created.json()
    const path = `/u/${participantToken}`
    const dayId = (await (await page.request.get(`/api/v1/polls/${participantToken}`)).json())
      .days[0].id

    for (let i = 0; i < 51; i++) {
      await page.request.post(`/api/v1/polls/${participantToken}/responses`, {
        data: { displayName: `Gast ${i}`, answers: [{ dayId, availability: 'no' }] },
      })
    }

    const context = await page.context().browser()!.newContext()
    const participant = await context.newPage()
    await participant.goto(path)

    // Half-written answer, before paging.
    await field(participant, 'participant-name').fill('Halb geschrieben')

    await participant.getByTestId('results-next').click()
    await expect(participant.getByTestId('result-row')).toHaveCount(1)

    // Paging reads the grid; it must not disturb the form above it (Principle I).
    await expect(field(participant, 'participant-name')).toHaveValue('Halb geschrieben')
    await expect(participant.getByTestId('answer-submit')).toBeVisible()

    await context.close()
  })
})
