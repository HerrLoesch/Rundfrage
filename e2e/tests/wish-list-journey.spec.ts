import { test, expect, type Browser, type Page } from '@playwright/test'
import { field } from '../support/fields'
import { gotoWishLists, revealWishListForm, signInToWishLists } from '../support/admin'

/**
 * The whole of feature 008 end to end: the operator asks for things, a group divides them up
 * through one link with no account, and the operator can change and supervise the list afterwards.
 */
test.describe('Wish list journey', () => {
  async function createList(
    page: Page,
    title: string,
    items: { name: string; wanted?: string }[],
  ): Promise<string> {
    await revealWishListForm(page)
    await field(page, 'wish-form-title').fill(title)
    await field(page, 'wish-form-target-date').fill('2099-07-18')

    for (const [index, item] of items.entries()) {
      if (index > 0) await page.getByTestId('wish-form-add-item').click()
      await field(page, `wish-form-item-name-${index}`).fill(item.name)
      if (item.wanted) await field(page, `wish-form-item-count-${index}`).fill(item.wanted)
    }

    await page.getByTestId('wish-form-submit').click()

    const link = await page.getByTestId('wish-share-url').textContent()
    return new URL(link!.trim()).pathname
  }

  /** A participant with no session at all - a fresh context, the way a link holder arrives. */
  async function claim(
    browser: Browser,
    path: string,
    name: string,
    item: string,
  ): Promise<{ personalLink: string; close: () => Promise<void> }> {
    const context = await browser.newContext()
    const page = await context.newPage()
    await page.goto(path)

    // The row this caller named, and the checkbox belonging to *that* row. Taking the first
    // checkbox on the page instead would claim whatever happens to be at the top, and the test
    // would still pass while asserting nothing about the item it names.
    const row = page.getByTestId('wish-items').locator('[data-testid^="wish-item-"]')
      .filter({ hasText: item }).first()
    await expect(row).toBeVisible()

    await row.locator('[data-testid^="wish-choose-"] input').check()
    await field(page, 'wish-name').fill(name)
    await page.getByTestId('wish-submit').click()

    await expect(page.getByTestId('wish-submitted')).toBeVisible()
    const personalLink = (await page.getByTestId('claim-url').textContent())!.trim()

    return { personalLink: new URL(personalLink).pathname, close: () => context.close() }
  }

  test('a group divides up what needs bringing, from one link and with no account', async ({
    page,
    browser,
  }) => {
    // US1: the whole loop, plus the capacity that makes it more than a shared note.
    const title = `Sommerfest ${Date.now()}`
    await signInToWishLists(page)
    const path = await createList(page, title, [{ name: 'Kuchen', wanted: '2' }, { name: 'Grill' }])

    const first = await claim(browser, path, 'Anna', 'Kuchen')
    await first.close()

    const second = await claim(browser, path, 'Ben', 'Kuchen')

    // The item is now complete, in words, and offers nobody a third place (FR-017, FR-018).
    const third = await browser.newContext()
    const thirdPage = await third.newPage()
    await thirdPage.goto(path)
    await expect(thirdPage.getByText('Vollständig').first()).toBeVisible()
    await expect(thirdPage.locator('[data-testid^="wish-choose-"]')).toHaveCount(1)
    await third.close()

    // Withdrawing through the personal link frees the place again (FR-022a).
    const personal = await browser.newContext()
    const personalPage = await personal.newPage()
    await personalPage.goto(second.personalLink)
    await personalPage.locator('[data-testid^="claim-withdraw-"]').first().click()
    await personalPage.getByTestId('claim-withdraw-confirmed').click()
    await expect(personalPage.getByTestId('claim-withdrawn')).toBeVisible()
    await personal.close()
    await second.close()

    const after = await browser.newContext()
    const afterPage = await after.newPage()
    await afterPage.goto(path)
    await expect(afterPage.locator('[data-testid^="wish-choose-"]')).toHaveCount(2)
    await after.close()
  })

  test('the operator edits the list afterwards, and the link keeps working', async ({
    page,
    browser,
  }) => {
    // US2: renaming keeps the names, and lowering a count below them is refused with the number.
    const title = `Herbstfest ${Date.now()}`
    await signInToWishLists(page)
    const path = await createList(page, title, [{ name: 'Kuchen', wanted: '3' }])

    const entry = await claim(browser, path, 'Anna', 'Kuchen')
    await entry.close()

    await gotoWishLists(page)
    await page.getByTestId('wish-list-row').filter({ hasText: title })
      .locator('[data-testid^="wish-open-"]').click()

    // Read *after* the navigation has landed. Read before, this captures the list's address and
    // then asserts the detail page is still at it - which it is not, and should not be.
    await expect(page.getByTestId('wish-detail-title')).toHaveText(title)
    const address = page.url()

    // SC-008: the address is the page, and a reload proves it.
    await page.reload()
    await expect(page.getByTestId('wish-detail-title')).toHaveText(title)
    expect(page.url()).toBe(address)

    const itemName = page.locator('[data-testid^="wish-item-name-"]').first().locator('input')
    await itemName.fill('Kuchen und Torte')
    await itemName.blur()
    await expect(page.getByText('Anna').first()).toBeVisible()

    // FR-033: below the names already entered, the refusal names them.
    const count = page.locator('[data-testid^="wish-item-count-"]').first().locator('input')
    await count.fill('0')
    await count.blur()
    await expect(page.getByTestId('wish-detail-problem')).toContainText('1')
  })

  test('the area and the dashboard report the same state', async ({ page, browser }) => {
    // US3, FR-049: one projection, two screens.
    const title = `Zahlen ${Date.now()}`
    await signInToWishLists(page)
    const path = await createList(page, title, [{ name: 'Kuchen', wanted: '2' }])

    // A name no operator would give an Ersteller, and that is the point rather than a flourish.
    //
    // This assertion used to name the participant "Anna" and check the whole dashboard section for
    // that string. Feature 009 added an owner column, and 009 FR-042 permits an ERSTELLER's name
    // there on the same grounds 008 FR-048b permits wish-list titles - so the moment somebody
    // issued a link to an Anna, this test failed while 008 FR-050 was perfectly intact. The
    // requirement is unchanged; the test could simply no longer tell the two kinds of name apart.
    const participantName = `Teilnehmerin-${Date.now()}`
    const entry = await claim(browser, path, participantName, 'Kuchen')
    await entry.close()

    // Both screens are read after *returning* to them, which is what FR-053 asks of the dashboard
    // and what makes the comparison meaningful: the claim arrived in another browser while this
    // page sat on the wish-list area, and neither screen re-reads without being entered again.
    await page.getByTestId('nav-dashboard').click()
    const dashboardRow = page.getByTestId('dashboard-wish-list-row').filter({ hasText: title })
    await expect(dashboardRow).toContainText('50')

    // FR-050: PARTICIPANT names stay off the dashboard. An Ersteller's name is operator-written
    // text and is allowed to be there (009 FR-042), which is why this names the participant
    // exactly rather than looking for any name-shaped string.
    await expect(page.getByTestId('dashboard-wish-lists')).not.toContainText(participantName)
    await expect(dashboardRow).not.toContainText(participantName)

    // FR-049: the area says the same thing, because it reads the same projection.
    await gotoWishLists(page)
    const row = page.getByTestId('wish-list-row').filter({ hasText: title })
    await expect(row).toContainText('50')
  })

  test('a narrow screen can still claim an item', async ({ page, browser }) => {
    // SC-010: 375 px, the width the specification names.
    const title = `Schmal ${Date.now()}`
    await signInToWishLists(page)
    const path = await createList(page, title, [{ name: 'Kuchen', wanted: '2' }])

    const context = await browser.newContext({ viewport: { width: 375, height: 720 } })
    const guest = await context.newPage()
    await guest.goto(path)

    await guest.locator('[data-testid^="wish-choose-"]').first().locator('input').check()
    await field(guest, 'wish-name').fill('Anna')
    await guest.getByTestId('wish-submit').click()

    await expect(guest.getByTestId('wish-submitted')).toBeVisible()
    await context.close()
  })
})
