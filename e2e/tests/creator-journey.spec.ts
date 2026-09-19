import { test, expect, type Browser, type Page } from '@playwright/test'
import { field } from '../support/fields'
import { gotoPolls, signInToCreators } from '../support/admin'

/**
 * The whole of feature 009 end to end: the operator hands somebody a link, that person makes their
 * own things with it, and the operator can take the link away without destroying what was made.
 *
 * This is the hand-walk quickstart.md describes, as a spec.
 */
test.describe('Creator link journey', () => {
  /** Issues an Ersteller and returns the path of its link. */
  async function issueLink(page: Page, name: string): Promise<string> {
    await page.getByTestId('creator-create-open').click()
    await field(page, 'creator-name-input').fill(name)
    await page.getByTestId('creator-create-submit').click()

    const row = page.getByTestId('creator-row').filter({ hasText: name })
    await expect(row).toBeVisible()

    const link = await row.getByTestId('creator-link').textContent()
    return new URL(link!.trim()).pathname
  }

  /** A holder arriving the way a holder does: a fresh context, no session, no cookie. */
  async function asHolder(browser: Browser, path: string) {
    const context = await browser.newContext()
    const page = await context.newPage()
    await page.goto(path)
    return { page, close: () => context.close() }
  }

  test('a link is issued, used, revoked, reissued and finally deleted', async ({ page, browser }) => {
    await signInToCreators(page)

    const name = `Anna ${Date.now()}`
    // Unique per run. The e2e suite shares one volume across specs, and a fixed title collides
    // with whatever an earlier journey left behind - "Sommerfest" is a substring of the
    // wish-list journey's "Sommerfest im Garten", which made the locator match two rows.
    const listTitle = `Ersteller-Fest ${Date.now()}`
    const linkPath = await issueLink(page, name)

    // --- The holder arrives with no session at all (FR-006) ---------------------------------
    const holder = await asHolder(browser, linkPath)

    await expect(holder.page.getByTestId('creator-greeting')).toContainText(name)

    // Never a sign-in form: there is no step between the link and the work.
    await expect(holder.page.getByTestId('sign-in-user')).toHaveCount(0)
    await expect(holder.page.getByTestId('admin-nav')).toHaveCount(0)

    // What the link grants, said before anything is shared (FR-058).
    await expect(holder.page.getByTestId('creator-link-warning')).toBeVisible()

    // Both lists, both empty and both saying so (US1 scenario 4).
    await expect(holder.page.getByTestId('creator-polls-empty')).toBeVisible()
    await expect(holder.page.getByTestId('creator-wish-lists-empty')).toBeVisible()

    // --- They make a wish list, and a stranger can answer it ---------------------------------
    await holder.page.getByTestId('creator-new-wish-list').click()
    await field(holder.page, 'creator-wish-list-title').fill(listTitle)
    await field(holder.page, 'creator-wish-list-date').fill('2099-07-18')
    await field(holder.page, 'creator-wish-list-items').fill('Kuchen, Getränke')
    await holder.page.getByTestId('creator-wish-list-submit').click()

    const listRow = holder.page.getByTestId('creator-wish-list-row')
    await expect(listRow).toBeVisible()

    const participantLink = await listRow.locator('a').first().getAttribute('href')
    const participant = await asHolder(browser, new URL(participantLink!).pathname)
    await expect(participant.page.getByTestId('wish-items')).toBeVisible()
    await participant.close()

    // --- Exactly one address carries the token (FR-028d, FR-028e, SC-011a) -------------------
    const addressBefore = holder.page.url()
    await holder.page.getByTestId('creator-open-wish-list').click()
    await expect(holder.page.getByTestId('creator-wish-list-detail')).toBeVisible()

    // Opened in place: the address did not change, and both lists are still around it.
    expect(holder.page.url()).toBe(addressBefore)
    await expect(holder.page.getByTestId('creator-polls')).toBeVisible()
    await expect(holder.page.getByTestId('creator-wish-lists')).toBeVisible()

    await holder.page.getByTestId('creator-close-wish-list').click()
    expect(holder.page.url()).toBe(addressBefore)

    // --- The operator revokes, and nothing is destroyed (FR-018, FR-020, SC-006a) ------------
    await page.reload()
    const row = page.getByTestId('creator-row').filter({ hasText: name })
    await row.getByTestId('creator-revoke').click()

    // The confirmation promises survival rather than announcing destruction.
    await expect(page.getByTestId('creator-revoke-confirm-body')).toContainText('bleiben erhalten')
    await page.getByTestId('delete-confirm-button').click()

    await expect(row.getByTestId('creator-no-link')).toBeVisible()

    // The link stops working immediately for its holder.
    await holder.page.reload()
    await expect(holder.page.getByTestId('creator-link-unusable')).toBeVisible()

    // But the wish list is still there, still theirs, and still answering participants.
    await gotoPolls(page)
    await page.getByTestId('nav-wish-lists').click()
    await expect(
      page.getByTestId('wish-list-row').filter({ hasText: listTitle }),
    ).toBeVisible()

    // --- A new link reaches exactly what they owned before (FR-016, SC-007) ------------------
    await page.getByTestId('nav-creators').click()
    const again = page.getByTestId('creator-row').filter({ hasText: name })
    await again.getByTestId('creator-reissue').click()
    await page.getByTestId('delete-confirm-button').click()

    const newLink = await again.getByTestId('creator-link').textContent()
    expect(new URL(newLink!.trim()).pathname).not.toBe(linkPath)

    const returned = await asHolder(browser, new URL(newLink!.trim()).pathname)
    await expect(returned.page.getByTestId('creator-wish-list-row')).toContainText(listTitle)
    await returned.close()

    // --- Deleting names both counts before it destroys anything (FR-020a, FR-020c) -----------
    await again.getByTestId('creator-delete').click()
    const body = page.getByTestId('creator-delete-confirm-body')
    await expect(body).toContainText(name)
    await expect(page.getByTestId('creator-delete-confirm-note')).toBeVisible()
    await page.getByTestId('delete-confirm-button').click()

    await expect(page.getByTestId('creator-row').filter({ hasText: name })).toHaveCount(0)

    await holder.close()
  })

  test('the participant surface is untouched by any of this (FR-015, FR-048)', async ({
    page,
    browser,
  }) => {
    // A guard, expected to pass on its first run: nothing in feature 009 touches this surface.
    // It exists to fail if somebody later leaks an owner's name into it.
    await signInToCreators(page)

    const name = `Ben ${Date.now()}`
    const linkPath = await issueLink(page, name)

    const holder = await asHolder(browser, linkPath)
    await holder.page.getByTestId('creator-new-poll').click()
    await field(holder.page, 'creator-poll-title').fill('Grillabend')
    await field(holder.page, 'creator-poll-days').fill('2099-12-01, 2099-12-02')
    await holder.page.getByTestId('creator-poll-submit').click()

    const pollRow = holder.page.getByTestId('creator-poll-row')
    await expect(pollRow).toBeVisible()
    const pollLink = await pollRow.locator('a').first().getAttribute('href')

    const participant = await asHolder(browser, new URL(pollLink!).pathname)

    // Feature 002's surface exactly: the poll, and nothing about who made it.
    await expect(participant.page.getByTestId('answer-form')).toBeVisible()
    await expect(participant.page.locator('body')).not.toContainText(name)
    await expect(participant.page.getByTestId('admin-nav')).toHaveCount(0)
    await expect(participant.page.getByTestId('creator-greeting')).toHaveCount(0)

    // And no route into any creator or admin surface.
    const hrefs = await participant.page.locator('a').evaluateAll((links) =>
      links.map((link) => link.getAttribute('href') ?? ''),
    )
    expect(hrefs.some((href) => href.startsWith('/admin') || href.startsWith('/e/'))).toBe(false)

    await participant.close()
    await holder.close()
  })

  test('a creator link reaches no installation-wide control (FR-029, SC-004)', async ({
    page,
    browser,
  }) => {
    await signInToCreators(page)
    const linkPath = await issueLink(page, `Carla ${Date.now()}`)

    const holder = await asHolder(browser, linkPath)

    // Nothing offered on the surface...
    await expect(holder.page.locator('input[type="file"]')).toHaveCount(0)
    await expect(holder.page.getByTestId('settings-maintenance')).toHaveCount(0)

    // ...and nothing reachable by asking the server directly either.
    for (const path of ['/api/v1/admin/creators', '/api/v1/admin/backup', '/api/v1/admin/dashboard']) {
      const response = await holder.page.request.get(path)
      expect(response.status()).toBe(401)
    }

    await holder.close()
  })
})
