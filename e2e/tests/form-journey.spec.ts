import { test, expect } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { field } from '../support/fields'
import { gotoForms, signInToForms } from '../support/admin'

/**
 * The whole of feature 010 end to end: the operator builds a typed form on the canvas, a
 * participant with no account is blocked from submitting anything incomplete, and the operator
 * takes the answers back out as CSV and JSON.
 */
test.describe('Form journey', () => {
  test('a form is built, answered with no account, and exported both ways', async ({
    page,
    context,
  }) => {
    const title = `Anmeldung ${Date.now()}`

    // US1: build the form.
    await signInToForms(page)
    await page.getByTestId('form-create-open').click()
    await field(page, 'form-create-title').fill(title)
    await page.getByTestId('form-create-submit').click()

    // The builder opens on the new, empty form - no link is offered yet (010 FR-011).
    await expect(page.getByTestId('form-builder-title')).toHaveText(title)
    await expect(page.getByTestId('form-builder-no-link')).toBeVisible()

    async function addField(
      type: string,
      label: string,
      required: boolean,
      maxLength?: string,
    ): Promise<void> {
      await page.getByTestId('form-add-field').click()
      await page.getByTestId('form-add-field-type').click()
      await page.getByRole('option', { name: new RegExp(type, 'i') }).first().click()
      await field(page, 'form-add-field-label').fill(label)
      if (required) await page.getByTestId('form-add-field-required').locator('input').check()
      if (maxLength) await field(page, 'form-add-field-max-length').fill(maxLength)
      await page.getByTestId('form-add-field-submit').click()
      await expect(page.getByTestId('form-add-field-dialog')).toBeHidden()
    }

    await addField('Text', 'Name', true, '100')
    await addField('E-Mail', 'E-Mail', true)
    await addField('Ja/Nein', 'Kommst du?', true)
    await addField('Ganzzahl', 'Personen', false)

    await expect(page.getByTestId('form-field-card')).toHaveCount(4)

    // FR-007: reorder with the move-up button - the keyboard-operable alternative to dragging
    // (FR-049), exercised here because it is the one every environment can drive reliably.
    const cards = page.getByTestId('form-field-card')
    await cards.nth(1).getByTestId('form-field-move-up').click()

    // The labels moved: E-Mail now precedes Name.
    await expect(cards.nth(0).getByTestId('form-field-label-input').locator('input'))
      .toHaveValue('E-Mail')

    // The link now appears, once a field exists (010 FR-011).
    await expect(page.getByTestId('form-builder-link')).toBeVisible()
    const link = (await page.getByTestId('form-builder-link').textContent())!.trim()
    const formPath = new URL(link).pathname

    // US2: a participant with no account, no session, and blocked from an incomplete answer.
    const guestContext = await context.browser()!.newContext()
    const guest = await guestContext.newPage()
    await guest.goto(formPath)

    await expect(guest.getByTestId('form-fill-title')).toHaveText(title)

    // Submitting with every required field untouched is blocked, client-side, before anything
    // is sent to the server (010 FR-016).
    await guest.getByTestId('form-fill-submit').click()
    await expect(guest.getByTestId('form-fill-field-error').first()).toBeVisible()

    // Fill in everything required (E-Mail is first after the reorder, then Name, then RSVP).
    const guestFields = guest.getByTestId('form-fill-field')
    await guestFields.nth(0).locator('input').fill('anna@example.com')
    await guestFields.nth(1).locator('input').fill('Anna')
    await guest.getByRole('radio', { name: /^Ja$/ }).check()

    await guest.getByTestId('form-fill-submit').click()
    await expect(guest.getByTestId('form-fill-confirmation')).toBeVisible()
    await guestContext.close()

    // Back in the builder: the response is there.
    await page.reload()
    await expect(page.getByTestId('form-response-row')).toHaveCount(1)

    // A second, real response, so the export has more than one row.
    const secondContext = await context.browser()!.newContext()
    const second = await secondContext.newPage()
    await second.goto(formPath)
    const secondFields = second.getByTestId('form-fill-field')
    await secondFields.nth(0).locator('input').fill('ben@example.com')
    await secondFields.nth(1).locator('input').fill('Ben')
    await second.getByRole('radio', { name: /^Nein$/ }).check()
    await second.getByTestId('form-fill-submit').click()
    await expect(second.getByTestId('form-fill-confirmation')).toBeVisible()
    await secondContext.close()

    await page.reload()
    await expect(page.getByTestId('form-response-row')).toHaveCount(2)

    // US3: both export formats.
    const csvDownload = await Promise.all([
      page.waitForEvent('download'),
      page.getByTestId('form-export-csv').click(),
    ]).then(([d]) => d)
    const csv = readFileSync(await csvDownload.path(), 'utf8')
    expect(csv).toContain('Anna')
    expect(csv).toContain('Ben')

    const jsonDownload = await Promise.all([
      page.waitForEvent('download'),
      page.getByTestId('form-export-json').click(),
    ]).then(([d]) => d)
    const document = JSON.parse(readFileSync(await jsonDownload.path(), 'utf8'))
    expect(document.responses).toHaveLength(2)

    // US4: delete one response, then the whole form.
    await page.getByTestId('form-response-delete').first().click()
    await page.getByTestId('delete-confirm-button').click()
    await expect(page.getByTestId('form-response-row')).toHaveCount(1)

    await gotoForms(page)
    const row = page.getByTestId('form-row').filter({ hasText: title })
    await row.getByTestId('form-delete').click()
    await page.getByTestId('delete-confirm-button').click()
    await expect(page.getByTestId('form-row').filter({ hasText: title })).toHaveCount(0)

    // The deleted form's link now answers like an unknown one (010 FR-021).
    const afterContext = await context.browser()!.newContext()
    const after = await afterContext.newPage()
    await after.goto(formPath)
    await expect(after.getByTestId('form-fill-unavailable')).toBeVisible()
    await afterContext.close()
  })

  test('a narrow screen can still complete and submit a form', async ({ page, context }) => {
    // SC-004: 375 px, the width the specification names.
    const title = `Schmal ${Date.now()}`

    await signInToForms(page)
    await page.getByTestId('form-create-open').click()
    await field(page, 'form-create-title').fill(title)
    await page.getByTestId('form-create-submit').click()
    await expect(page.getByTestId('form-builder-title')).toHaveText(title)

    await page.getByTestId('form-add-field').click()
    await field(page, 'form-add-field-label').fill('Name')
    await page.getByTestId('form-add-field-required').locator('input').check()
    await page.getByTestId('form-add-field-submit').click()
    await expect(page.getByTestId('form-add-field-dialog')).toBeHidden()

    const link = (await page.getByTestId('form-builder-link').textContent())!.trim()
    const formPath = new URL(link).pathname

    const narrowContext = await context.browser()!.newContext({ viewport: { width: 375, height: 720 } })
    const guest = await narrowContext.newPage()
    await guest.goto(formPath)

    await guest.getByTestId('form-fill-field').first().locator('input').fill('Anna')
    await guest.getByTestId('form-fill-submit').click()

    await expect(guest.getByTestId('form-fill-confirmation')).toBeVisible()
    await narrowContext.close()
  })
})
