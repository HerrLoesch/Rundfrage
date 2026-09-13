import { expect, type Page } from '@playwright/test'
import { field } from './fields'
import { ADMIN_PASSWORD, ADMIN_USER } from './credentials'

/**
 * Getting into the admin area, in one place.
 *
 * Feature 007 turned the admin area into a shell with one area per capability, so "sign in" and
 * "be where the control I need lives" became two steps instead of one. Nine specs needed that
 * change; putting it here means the next rearrangement is one edit rather than nine.
 *
 * Signing in lands on the dashboard in every case (007 FR-011a) - the area the operator was
 * refused from is deliberately not remembered - so each helper below states where it goes next.
 */
export async function signIn(page: Page): Promise<void> {
  await page.goto('/admin')
  await field(page, 'sign-in-user').fill(ADMIN_USER)
  await field(page, 'sign-in-password').fill(ADMIN_PASSWORD)
  await page.getByTestId('sign-in-submit').click()

  // The dashboard is the landing area, and its navigation is the proof the shell is up.
  await expect(page.getByTestId('admin-nav')).toBeVisible()
}

/**
 * Opens the drawer if it is not permanent.
 *
 * Above Vuetify's md breakpoint the drawer is always there and the toggle does not exist; below
 * it the entries are present but off-screen until the drawer slides in (007 FR-012). A helper
 * that assumed the wide case clicked an entry that was never in the viewport.
 */
async function openNavigation(page: Page): Promise<void> {
  const toggle = page.getByTestId('admin-nav-toggle')
  if (await toggle.isVisible()) {
    await toggle.click()
    await expect(page.getByTestId('nav-polls')).toBeInViewport()
  }
}

/** The date-poll area: creating, listing, exporting and deleting polls (007 FR-014). */
export async function gotoPolls(page: Page): Promise<void> {
  await openNavigation(page)
  await page.getByTestId('nav-polls').click()
  await expect(page.getByTestId('poll-create-toggle')).toBeVisible()
}

/** The wish-list area: creating, listing and opening one wish list (008 FR-041). */
export async function gotoWishLists(page: Page): Promise<void> {
  await openNavigation(page)
  await page.getByTestId('nav-wish-lists').click()
  await expect(page.getByTestId('wish-create-toggle')).toBeVisible()
}

/** Signs in and goes straight to the wish-list area. */
export async function signInToWishLists(page: Page): Promise<void> {
  await signIn(page)
  await gotoWishLists(page)
}

/** Reveals the wish-list creation form, which is behind an action (008 FR-042). */
export async function revealWishListForm(page: Page): Promise<void> {
  await page.getByTestId('wish-create-toggle').click()
  await expect(page.getByTestId('wish-list-form')).toBeVisible()
}

/** Settings: maintenance mode, the backup download and the restore (007 FR-019). */
export async function gotoSettings(page: Page): Promise<void> {
  await openNavigation(page)
  await page.getByTestId('nav-settings').click()
  await expect(page.getByTestId('settings-maintenance')).toBeVisible()
}

/** Signs in and goes straight to the poll area, which is what most specs want. */
export async function signInToPolls(page: Page): Promise<void> {
  await signIn(page)
  await gotoPolls(page)
}

/** Signs in and goes straight to settings. */
export async function signInToSettings(page: Page): Promise<void> {
  await signIn(page)
  await gotoSettings(page)
}

/**
 * Reveals the poll creation form. It is behind an action now rather than permanently open
 * (007 FR-014h), which is the one step this feature deliberately added (SC-005).
 */
export async function revealPollForm(page: Page): Promise<void> {
  await page.getByTestId('poll-create-toggle').click()
  await expect(page.getByTestId('poll-form')).toBeVisible()
}

/** Reveals the import panel, likewise behind an action now (007 FR-014h). */
export async function revealImportPanel(page: Page): Promise<void> {
  await page.getByTestId('poll-import-toggle').click()
  await expect(page.getByTestId('import-panel')).toBeVisible()
}
