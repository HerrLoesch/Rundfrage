import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent, de } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  listForms: vi.fn(),
  fetchForm: vi.fn(),
  createForm: vi.fn(),
  renameForm: vi.fn(),
  deleteForm: vi.fn(),
  addFormField: vi.fn(),
  updateFormField: vi.fn(),
  removeFormField: vi.fn(),
  reorderFormFields: vi.fn(),
  listFormResponses: vi.fn(),
  deleteFormResponse: vi.fn(),
  formExportCsvUrl: vi.fn((id: string) => `/api/v1/admin/forms/${id}/export/csv`),
  formExportJsonUrl: vi.fn((id: string) => `/api/v1/admin/forms/${id}/export/json`),
}))

import { createRouter, createMemoryHistory } from 'vue-router'
import {
  fetchForm,
  addFormField,
  updateFormField,
  removeFormField,
  reorderFormFields,
  listFormResponses,
  deleteFormResponse,
  type FormDetail,
} from '../../src/api/client'
import { routes } from '../../src/router'
import FormBuilderView from '../../src/components/admin/FormBuilderView.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))
const router = createRouter({ history: createMemoryHistory(), routes })

const aForm = (overrides: Record<string, unknown> = {}): FormDetail => ({
  id: 'f1',
  title: 'Anmeldung',
  createdAt: '2026-09-22T00:00:00Z',
  fieldCount: 2,
  responseCount: 0,
  formToken: 'tok123456789012345678',
  fields: [
    { id: 'a', type: 'text', label: 'Name', required: true, maxLength: 100, minLength: null, displayOrder: 0 },
    { id: 'b', type: 'email', label: 'E-Mail', required: false, maxLength: null, minLength: null, displayOrder: 1 },
  ],
  ...overrides,
})

const mountView = () =>
  mountComponent(FormBuilderView, { props: { formId: 'f1' }, global: { plugins: [router] } })

describe('FormBuilderView', () => {
  beforeEach(() => {
    vi.mocked(fetchForm).mockReset()
    vi.mocked(fetchForm).mockResolvedValue(aForm())
    vi.mocked(listFormResponses).mockReset()
    vi.mocked(listFormResponses).mockResolvedValue([])
    vi.mocked(addFormField).mockReset()
    vi.mocked(updateFormField).mockReset()
    vi.mocked(removeFormField).mockReset()
    vi.mocked(removeFormField).mockResolvedValue(undefined)
    vi.mocked(reorderFormFields).mockReset()
  })

  it('shows every field with its type, label and required state', async () => {
    const wrapper = mountView()
    await flush()

    const cards = wrapper.findAll('[data-testid="form-field-card"]')
    expect(cards).toHaveLength(2)

    const labels = wrapper.findAll('[data-testid="form-field-label-input"] input')
      .map((input) => (input.element as HTMLInputElement).value)
    expect(labels).toEqual(['Name', 'E-Mail'])
  })

  it('offers the link once a field exists, and states so when none exist', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-builder-link"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="form-builder-no-link"]').exists()).toBe(false)
  })

  it('states that a field is needed first, on an empty form', async () => {
    vi.mocked(fetchForm).mockResolvedValue(aForm({ fieldCount: 0, fields: [] }))

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-builder-no-link"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="form-field-canvas-empty"]').exists()).toBe(true)
  })

  it('adds a field of the chosen type through the dialog', async () => {
    vi.mocked(addFormField).mockResolvedValue({
      id: 'c', type: 'integer', label: 'Alter', required: false, maxLength: null, minLength: null, displayOrder: 2,
    })

    document.body.innerHTML = ''
    const wrapper = mountComponent(
      FormBuilderView,
      { props: { formId: 'f1' }, global: { plugins: [router] }, attachTo: document.body },
    )
    await flush()

    await wrapper.get('[data-testid="form-add-field"]').trigger('click')
    await flush()

    const labelInput = document.body.querySelector<HTMLInputElement>(
      '[data-testid="form-add-field-label"] input',
    )!
    labelInput.value = 'Alter'
    labelInput.dispatchEvent(new Event('input'))
    await flush()

    document.body.querySelector<HTMLButtonElement>('[data-testid="form-add-field-submit"]')?.click()
    await flush()

    expect(addFormField).toHaveBeenCalledWith('f1', expect.objectContaining({ label: 'Alter' }))
    document.body.innerHTML = ''
  })

  it('removes a field after a confirmation', async () => {
    document.body.innerHTML = ''
    const wrapper = mountComponent(
      FormBuilderView,
      { props: { formId: 'f1' }, global: { plugins: [router] }, attachTo: document.body },
    )
    await flush()

    await wrapper.get('[data-testid="form-field-remove"]').trigger('click')
    await flush()
    document.body.querySelector<HTMLButtonElement>('[data-testid="delete-confirm-button"]')?.click()
    await flush()

    expect(removeFormField).toHaveBeenCalledWith('f1', 'a')
    document.body.innerHTML = ''
  })

  it('reorders fields with the move-up and move-down buttons (FR-049 non-drag alternative)', async () => {
    const wrapper = mountView()
    await flush()

    const downButtons = wrapper.findAll('[data-testid="form-field-move-down"]')
    await downButtons[0]!.trigger('click')
    await flush()

    expect(reorderFormFields).toHaveBeenCalledWith('f1', ['b', 'a'])
  })

  it('shows the new field order immediately, before the reorder request resolves (SC-007)', async () => {
    // No round trip required to see the new order in the builder: resolve the mocked request
    // only after the DOM assertion, so a store that waited for the response would fail here.
    let resolveReorder!: (value: FormDetail) => void
    vi.mocked(reorderFormFields).mockReturnValue(
      new Promise((resolve) => { resolveReorder = resolve }),
    )

    const wrapper = mountView()
    await flush()

    const downButtons = wrapper.findAll('[data-testid="form-field-move-down"]')
    await downButtons[0]!.trigger('click')
    await flush()

    const labelsWhilePending = wrapper.findAll('[data-testid="form-field-label-input"] input')
      .map((input) => (input.element as HTMLInputElement).value)
    expect(labelsWhilePending).toEqual(['E-Mail', 'Name'])

    resolveReorder(aForm({
      fields: [
        { id: 'b', type: 'email', label: 'E-Mail', required: false, maxLength: null, minLength: null, displayOrder: 0 },
        { id: 'a', type: 'text', label: 'Name', required: true, maxLength: 100, minLength: null, displayOrder: 1 },
      ],
    }))
    await flush()
  })

  it('accepts a maximum length of exactly 0, rather than silently treating it as unset', async () => {
    // `Number("0") || null` evaluates to null because 0 is falsy - typing "0" and blurring must
    // send 0, not silently keep the field's previous value with no error shown.
    vi.mocked(updateFormField).mockResolvedValue({
      id: 'a', type: 'text', label: 'Name', required: true, maxLength: 0, minLength: null, displayOrder: 0,
    })

    const wrapper = mountView()
    await flush()

    const maxLengthInput = wrapper.get('[data-testid="form-field-max-length-input"] input')
    await maxLengthInput.setValue('0')
    await maxLengthInput.trigger('blur')
    await flush()

    expect(updateFormField).toHaveBeenCalledWith('f1', 'a', expect.objectContaining({ maxLength: 0 }))
  })

  it('shows a visible error when renaming or editing a field is rejected', async () => {
    // store.problem was only ever displayed inside the add-field dialog; a rejected rename or
    // label edit set it with nothing on screen to show for it.
    vi.mocked(updateFormField).mockRejectedValue({ code: 'label_too_long', limit: 200 })

    const wrapper = mountView()
    await flush()

    const labelInput = wrapper.get('[data-testid="form-field-label-input"] input')
    await labelInput.setValue('x'.repeat(201))
    await labelInput.trigger('blur')
    await flush()

    expect(wrapper.get('[data-testid="form-builder-error"]').text()).toContain('200')
  })

  it('states the real number of collected values before removing a field, not always zero', async () => {
    vi.mocked(listFormResponses).mockResolvedValue([
      { id: 'r1', submittedAt: '2026-09-22T00:00:00Z', values: [{ fieldId: 'a', value: 'Anna' }] },
      { id: 'r2', submittedAt: '2026-09-22T00:00:01Z', values: [{ fieldId: 'a', value: 'Ben' }] },
      { id: 'r3', submittedAt: '2026-09-22T00:00:02Z', values: [{ fieldId: 'b', value: 'x@example.com' }] },
    ])

    document.body.innerHTML = ''
    const wrapper = mountComponent(
      FormBuilderView,
      { props: { formId: 'f1' }, global: { plugins: [router] }, attachTo: document.body },
    )
    await flush()

    await wrapper.get('[data-testid="form-field-remove"]').trigger('click')
    await flush()

    const body = document.body.querySelector('[data-testid="form-field-remove-confirm-body"]')?.textContent
    expect(body).toContain('2')
    document.body.innerHTML = ''
  })

  it('disables moving the first field up and the last field down', async () => {
    const wrapper = mountView()
    await flush()

    const upButtons = wrapper.findAll('[data-testid="form-field-move-up"]')
    const downButtons = wrapper.findAll('[data-testid="form-field-move-down"]')

    expect(upButtons[0]!.attributes('disabled')).toBeDefined()
    expect(downButtons[downButtons.length - 1]!.attributes('disabled')).toBeDefined()
  })

  it('offers both CSV and JSON export', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="form-export-csv"]').attributes('href')).toContain('/export/csv')
    expect(wrapper.get('[data-testid="form-export-json"]').attributes('href')).toContain('/export/json')
  })

  it('shows the empty state when there are no responses yet', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-responses-empty"]').exists()).toBe(true)
  })

  it('lists responses and can delete one after a confirmation', async () => {
    vi.mocked(listFormResponses).mockResolvedValue([
      { id: 'r1', submittedAt: '2026-09-22T00:00:00Z', values: [{ fieldId: 'a', value: 'Anna' }] },
    ])
    vi.mocked(deleteFormResponse).mockResolvedValue(undefined)

    document.body.innerHTML = ''
    const wrapper = mountComponent(
      FormBuilderView,
      { props: { formId: 'f1' }, global: { plugins: [router] }, attachTo: document.body },
    )
    await flush()

    expect(wrapper.find('[data-testid="form-response-row"]').exists()).toBe(true)

    await wrapper.get('[data-testid="form-response-delete"]').trigger('click')
    await flush()
    document.body.querySelector<HTMLButtonElement>('[data-testid="delete-confirm-button"]')?.click()
    await flush()

    expect(deleteFormResponse).toHaveBeenCalledWith('f1', 'r1')
    document.body.innerHTML = ''
  })

  it('shows responses as a table with one column per field, in the builder order', async () => {
    vi.mocked(listFormResponses).mockResolvedValue([
      {
        id: 'r1', submittedAt: '2026-09-22T00:00:00Z',
        values: [{ fieldId: 'a', value: 'Anna' }, { fieldId: 'b', value: 'anna@example.com' }],
      },
      {
        // No value for "b" - the field was added after this response was submitted (010 FR-038).
        id: 'r2', submittedAt: '2026-09-22T00:00:01Z',
        values: [{ fieldId: 'a', value: 'Ben' }],
      },
    ])

    const wrapper = mountView()
    await flush()

    const table = wrapper.get('[data-testid="form-responses"]')
    const headers = table.findAll('thead th').map((h) => h.text())
    expect(headers).toEqual(['Eingegangen am', 'Name', 'E-Mail', 'Antwort löschen'])

    const rows = table.findAll('tbody tr')
    expect(rows).toHaveLength(2)

    const firstRowCells = rows[0]!.findAll('[data-testid="form-response-cell"]').map((c) => c.text())
    expect(firstRowCells).toEqual(['Anna', 'anna@example.com'])

    // The field added after this response has no value for it - an empty cell, not a guessed one.
    const secondRowCells = rows[1]!.findAll('[data-testid="form-response-cell"]').map((c) => c.text())
    expect(secondRowCells).toEqual(['Ben', ''])
  })

  it('keeps a refused label as typed and marks it, rather than pretending it was saved', async () => {
    vi.mocked(updateFormField).mockRejectedValue({ code: 'label_too_long', limit: 200 })

    const wrapper = mountView()
    await flush()

    const labelField = wrapper.findAll('[data-testid="form-field-label-input"]')[0]!
    const tooLong = 'x'.repeat(201)
    await labelField.get('input').setValue(tooLong)
    await labelField.get('input').trigger('blur')
    await flush()

    // Still what the operator typed, so it can be shortened - and visibly not accepted.
    expect((labelField.get('input').element as HTMLInputElement).value).toBe(tooLong)
    expect(labelField.classes()).toContain('v-input--error')
  })

  it('clears the refused mark once the corrected label is saved', async () => {
    vi.mocked(updateFormField)
      .mockRejectedValueOnce({ code: 'label_too_long', limit: 200 })
      .mockResolvedValueOnce({
        id: 'a', type: 'text', label: 'Vorname', required: true, maxLength: 100, minLength: null, displayOrder: 0,
      })

    const wrapper = mountView()
    await flush()
    const labelField = wrapper.findAll('[data-testid="form-field-label-input"]')[0]!

    await labelField.get('input').setValue('x'.repeat(201))
    await labelField.get('input').trigger('blur')
    await flush()
    await labelField.get('input').setValue('Vorname')
    await labelField.get('input').trigger('blur')
    await flush()

    expect(labelField.classes()).not.toContain('v-input--error')
    expect((labelField.get('input').element as HTMLInputElement).value).toBe('Vorname')
  })

  it('puts the stored label back when the input is emptied, without sending anything', async () => {
    // A label is required (FR-004); an emptied input is not an edit to make.
    const wrapper = mountView()
    await flush()

    const input = wrapper.findAll('[data-testid="form-field-label-input"] input')[0]!
    await input.setValue('')
    await input.trigger('blur')
    await flush()

    expect(updateFormField).not.toHaveBeenCalled()
    expect((input.element as HTMLInputElement).value).toBe('Name')
  })

  it('clears a minimum length when its input is emptied, and keeps a typed 0 as 0', async () => {
    vi.mocked(fetchForm).mockResolvedValue(aForm({
      fields: [
        { id: 'a', type: 'text', label: 'Name', required: true, maxLength: 100, minLength: 5, displayOrder: 0 },
      ],
    }))
    vi.mocked(updateFormField).mockImplementation(async (_form, _field, patch) => ({
      id: 'a', type: 'text', label: 'Name', required: true,
      maxLength: patch.maxLength ?? 100, minLength: patch.minLength ?? null, displayOrder: 0,
    }))

    const wrapper = mountView()
    await flush()
    const minInput = wrapper.get('[data-testid="form-field-min-length-input"] input')

    await minInput.setValue('')
    await minInput.trigger('blur')
    await flush()
    expect(updateFormField).toHaveBeenLastCalledWith('f1', 'a', { maxLength: 100, minLength: null })

    await minInput.setValue('0')
    await minInput.trigger('blur')
    await flush()
    expect(updateFormField).toHaveBeenLastCalledWith('f1', 'a', { maxLength: 100, minLength: 0 })
  })

  it('sends nothing when a length input is left without a change', async () => {
    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="form-field-max-length-input"] input').trigger('blur')
    await flush()

    expect(updateFormField).not.toHaveBeenCalled()
  })

  it('moves a field with the arrow keys on its drag handle, so dragging is not the only way', async () => {
    const wrapper = mountView()
    await flush()

    await wrapper.findAll('[data-testid="form-field-drag-handle"]')[0]!.trigger('keydown', { key: 'ArrowDown' })
    await flush()

    expect(reorderFormFields).toHaveBeenCalledWith('f1', ['b', 'a'])
  })

  it('heads the field list as the fields, not as the add-field action', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.findAll('h2').map((h) => h.text())).toContain(de.form.fieldsTitle)
  })
})
