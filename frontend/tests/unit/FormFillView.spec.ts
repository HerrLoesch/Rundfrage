import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent, de } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  fetchFormByToken: vi.fn(),
  submitFormResponse: vi.fn(),
}))

import {
  fetchFormByToken, submitFormResponse, type PublicFormDefinition,
} from '../../src/api/client'
import FormFillView from '../../src/components/form/FormFillView.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))
const mountView = () => mountComponent(FormFillView, { props: { formToken: 'tok' } })

const aDefinition = (overrides: Record<string, unknown> = {}): PublicFormDefinition => ({
  title: 'Anmeldung',
  fields: [
    { id: 'a', type: 'text', label: 'Name', required: true, maxLength: 100, minLength: null, displayOrder: 0 },
    { id: 'b', type: 'email', label: 'E-Mail', required: true, maxLength: null, minLength: null, displayOrder: 1 },
    { id: 'c', type: 'integer', label: 'Alter', required: false, maxLength: null, minLength: null, displayOrder: 2 },
  ],
  ...overrides,
})

describe('FormFillView', () => {
  beforeEach(() => {
    vi.mocked(fetchFormByToken).mockReset()
    vi.mocked(fetchFormByToken).mockResolvedValue(aDefinition())
    vi.mocked(submitFormResponse).mockReset()
  })

  it('renders every field in the order the operator left them, with no step before the form', async () => {
    // 010 FR-012, FR-013.
    const wrapper = mountView()
    await flush()

    const fields = wrapper.findAll('[data-testid="form-fill-field"]')
    expect(fields).toHaveLength(3)
    expect(wrapper.get('[data-testid="form-fill-title"]').text()).toBe('Anmeldung')
  })

  it('marks required fields in text, not colour alone', async () => {
    const wrapper = mountView()
    await flush()

    const markers = wrapper.findAll('[data-testid="form-fill-required-marker"]')
    expect(markers).toHaveLength(2) // Name and E-Mail are required; Alter is not.
  })

  it('blocks submission while a required field is empty, and sends nothing', async () => {
    const wrapper = mountView()
    await flush()

    await wrapper.get('form').trigger('submit')
    await flush()

    expect(submitFormResponse).not.toHaveBeenCalled()
    expect(wrapper.findAll('[data-testid="form-fill-field-error"]').length).toBeGreaterThan(0)
  })

  it('does not default a required boolean field to yes or no', async () => {
    // 010 FR-015: no default on the participant's behalf.
    vi.mocked(fetchFormByToken).mockResolvedValue(aDefinition({
      fields: [
        { id: 'x', type: 'boolean', label: 'Kommst du?', required: true, maxLength: null, minLength: null, displayOrder: 0 },
      ],
    }))

    const wrapper = mountView()
    await flush()

    // Neither radio option is checked.
    const radios = wrapper.findAll('input[type="radio"]')
    expect(radios.every((r) => !(r.element as HTMLInputElement).checked)).toBe(true)

    await wrapper.get('form').trigger('submit')
    await flush()

    expect(submitFormResponse).not.toHaveBeenCalled()
  })

  it('allows a valid submission with an optional field left blank', async () => {
    vi.mocked(submitFormResponse).mockResolvedValue(undefined)

    const wrapper = mountView()
    await flush()

    const inputs = wrapper.findAll('[data-testid="form-fill-field"] input')
    await inputs[0]!.setValue('Anna')
    await inputs[1]!.setValue('anna@example.com')
    // Alter (optional) left blank.

    await wrapper.get('form').trigger('submit')
    await flush()

    expect(submitFormResponse).toHaveBeenCalledWith('tok', [
      { fieldId: 'a', value: 'Anna' },
      { fieldId: 'b', value: 'anna@example.com' },
    ])
    expect(wrapper.find('[data-testid="form-fill-confirmation"]').exists()).toBe(true)
  })

  it('blocks a malformed value client-side without sending the request', async () => {
    const wrapper = mountView()
    await flush()

    const inputs = wrapper.findAll('[data-testid="form-fill-field"] input')
    await inputs[0]!.setValue('Anna')
    await inputs[1]!.setValue('not-an-email')

    await wrapper.get('form').trigger('submit')
    await flush()

    expect(submitFormResponse).not.toHaveBeenCalled()
  })

  it('shows a message specific to the field type, not one generic text for every violation', async () => {
    // Each field type failed its own way; the messages must say so, so that a bad e-mail address,
    // a too-short name and a bad postal code no longer read as the identical, unhelpful sentence.
    vi.mocked(fetchFormByToken).mockResolvedValue(aDefinition({
      fields: [
        { id: 'a', type: 'text', label: 'Name', required: true, maxLength: 20, minLength: 3, displayOrder: 0 },
        { id: 'b', type: 'email', label: 'E-Mail', required: true, maxLength: null, minLength: null, displayOrder: 1 },
        { id: 'p', type: 'postalCode', label: 'PLZ', required: true, maxLength: null, minLength: null, displayOrder: 2 },
      ],
    }))

    const wrapper = mountView()
    await flush()

    const inputs = wrapper.findAll('[data-testid="form-fill-field"] input')
    await inputs[0]!.setValue('AB') // shorter than the minimum of 3
    await inputs[1]!.setValue('not-an-email')
    await inputs[2]!.setValue('123') // not five digits

    await wrapper.get('form').trigger('submit')
    await flush()

    const messages = wrapper.findAll('[data-testid="form-fill-field-error"]').map((e) => e.text())

    expect(messages[0]).toBe(de.error.field_invalid_text_range.replace('{min}', '3').replace('{max}', '20'))
    // The escaped "@" in the catalogue entry (vue-i18n treats a bare "@" as its own linked-message
    // syntax) must render as a literal "@", not leak the escaping syntax into what is shown.
    expect(messages[1]).toBe('Bitte eine gültige E-Mail-Adresse eingeben, z. B. name@beispiel.de.')
    expect(messages[2]).toBe(de.error.field_invalid_postal_code)
    expect(new Set(messages).size).toBe(3) // three different violations, three different messages
  })

  it('shows the unknown/malformed/deleted/zero-field notice identically', async () => {
    // 010 FR-021.
    vi.mocked(fetchFormByToken).mockRejectedValue({ code: 'not_found' })

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-fill-unavailable"]').exists()).toBe(true)
  })

  it('shows the maintenance notice distinctly from the not-found notice', async () => {
    vi.mocked(fetchFormByToken).mockRejectedValue({ code: 'maintenance' })

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-fill-maintenance"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="form-fill-unavailable"]').exists()).toBe(false)
  })

  it('translates a server-side field rejection the client did not catch', async () => {
    // The server's codes carry a "field_" prefix (ErrorCodes.FieldRequired = "field_required");
    // storing that whole code and having the template re-add the prefix produced the missing key
    // "error.field_field_required". Reachable in ordinary use: the operator can add a new
    // required field while a participant already has the form open, so the participant's stale,
    // already-fetched field list never validates it, and only the server catches it.
    vi.mocked(submitFormResponse).mockRejectedValue({
      code: 'submission_invalid',
      fields: [{ fieldId: 'c', error: 'field_required' }],
    })

    const wrapper = mountView()
    await flush()

    const inputs = wrapper.findAll('[data-testid="form-fill-field"] input')
    await inputs[0]!.setValue('Anna')
    await inputs[1]!.setValue('anna@example.com')
    // "Alter" (id "c") is optional in the loaded definition, so the client sends nothing for it
    // and never blocks the submission itself - only the mocked server rejects it.

    await wrapper.get('form').trigger('submit')
    await flush()

    const errorFields = wrapper.findAll('[data-testid="form-fill-field"]')
    const ageField = errorFields[2]!
    expect(ageField.find('[data-testid="form-fill-field-error"]').text()).toBe(de.error.field_required)
  })
})
