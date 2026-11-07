import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

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
}))

import {
  addFormField,
  createForm,
  deleteFormResponse,
  fetchForm,
  listFormResponses,
  listForms,
  removeFormField,
  updateFormField,
  type FormDetail,
  type FormFieldDetail,
} from '../../src/api/client'
import { useFormsStore } from '../../src/stores/forms'

const field = (id: string, displayOrder: number, label = id): FormFieldDetail => ({
  id, type: 'text', label, required: false, maxLength: 50, minLength: null, displayOrder,
})

const aForm = (): FormDetail => ({
  id: 'f1',
  title: 'Anmeldung',
  createdAt: '2026-09-22T00:00:00Z',
  fieldCount: 3,
  responseCount: 2,
  formToken: 'tok',
  fields: [field('a', 0), field('b', 1), field('c', 2)],
})

/**
 * The operator's form store applies each write from the server's own answer, rather than
 * re-reading the whole form after every edit - so these tests also pin that no second read
 * happens.
 */
describe('forms store', () => {
  beforeEach(async () => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    vi.mocked(fetchForm).mockResolvedValue(aForm())
    vi.mocked(listFormResponses).mockResolvedValue([
      { id: 'r1', submittedAt: '2026-09-22T10:00:00Z', values: [{ fieldId: 'a', value: 'x' }, { fieldId: 'b', value: 'y' }] },
      { id: 'r2', submittedAt: '2026-09-22T11:00:00Z', values: [{ fieldId: 'b', value: 'z' }] },
    ])
  })

  async function opened() {
    const store = useFormsStore()
    await store.open('f1')
    vi.mocked(fetchForm).mockClear()
    return store
  }

  it('appends an added field from the server answer and counts it', async () => {
    vi.mocked(addFormField).mockResolvedValue(field('d', 3, 'Neu'))
    const store = await opened()

    expect(await store.addField({ type: 'text', label: 'Neu', required: false, maxLength: 50, minLength: null })).toBe(true)

    expect(store.current!.fields.map((f) => f.id)).toEqual(['a', 'b', 'c', 'd'])
    expect(store.current!.fieldCount).toBe(4)
    expect(fetchForm).not.toHaveBeenCalled()
  })

  it('replaces an edited field in place with the server answer', async () => {
    vi.mocked(updateFormField).mockResolvedValue({ ...field('b', 1), label: 'Umbenannt' })
    const store = await opened()

    await store.updateField('b', { label: 'Umbenannt' })

    expect(store.current!.fields.map((f) => f.label)).toEqual(['a', 'Umbenannt', 'c'])
    expect(fetchForm).not.toHaveBeenCalled()
  })

  it('removes a field, closes the gap in the order and drops its values from every response', async () => {
    vi.mocked(removeFormField).mockResolvedValue(undefined)
    const store = await opened()

    await store.removeField('b')

    expect(store.current!.fields.map((f) => [f.id, f.displayOrder])).toEqual([['a', 0], ['c', 1]])
    expect(store.current!.fieldCount).toBe(2)
    // The same cascade the server ran (010 FR-009): r2 held a value only for "b".
    expect(store.responses.map((r) => r.values.map((v) => v.fieldId))).toEqual([['a'], []])
  })

  it('takes a deleted response out of the list and out of the count', async () => {
    vi.mocked(deleteFormResponse).mockResolvedValue(undefined)
    const store = await opened()

    await store.deleteResponse('r1')

    expect(store.responses.map((r) => r.id)).toEqual(['r2'])
    expect(store.current!.responseCount).toBe(1)
    expect(fetchForm).not.toHaveBeenCalled()
  })

  it('leaves the form exactly as it was and records the problem when a write is refused', async () => {
    vi.mocked(updateFormField).mockRejectedValue({ code: 'label_too_long', limit: 200 })
    const store = await opened()
    const before = store.current

    expect(await store.updateField('a', { label: 'x'.repeat(201) })).toBe(false)

    expect(store.current).toBe(before)
    expect(store.problem).toEqual({ code: 'label_too_long', limit: 200 })
  })

  it('clears the previous problem as soon as the next write succeeds', async () => {
    vi.mocked(updateFormField)
      .mockRejectedValueOnce({ code: 'label_required' })
      .mockResolvedValueOnce(field('a', 0, 'Gut'))
    const store = await opened()

    await store.updateField('a', { label: '' })
    await store.updateField('a', { label: 'Gut' })

    expect(store.problem).toBeNull()
  })

  it('returns a created form without re-reading the list it is about to leave', async () => {
    vi.mocked(createForm).mockResolvedValue({ ...aForm(), id: 'f2', fields: [], fieldCount: 0 })
    const store = useFormsStore()

    const created = await store.create('Neu')

    expect(created?.id).toBe('f2')
    expect(listForms).not.toHaveBeenCalled()
  })
})
