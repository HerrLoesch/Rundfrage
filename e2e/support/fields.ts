import type { Locator, Page } from '@playwright/test'

/**
 * Vuetify puts a fallthrough attribute on its wrapper element, not on the control inside, so
 * `data-testid` identifies the *field* and this reaches the input a person actually types into.
 *
 * Keeping the indirection here rather than in every spec means a future change to how fields
 * are rendered is one edit, not thirty.
 */
export const field = (page: Page | Locator, testId: string): Locator =>
  page.getByTestId(testId).locator('input')

export const textarea = (page: Page | Locator, testId: string): Locator =>
  page.getByTestId(testId).locator('textarea')

/** A Vuetify radio renders a real <input type="radio"> - see research.md R-11. */
export const radio = (scope: Locator, testId: string): Locator =>
  scope.getByTestId(testId).locator('input[type="radio"]')
