import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent } from '../support/mount'

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
  formExportCsvUrl: vi.fn(() => '/api/v1/admin/forms/f1/export/csv'),
  formExportJsonUrl: vi.fn(() => '/api/v1/admin/forms/f1/export/json'),
}))

import { createRouter, createMemoryHistory } from 'vue-router'
import { listForms, createForm, deleteForm } from '../../src/api/client'
import { routes } from '../../src/router'
import FormsView from '../../src/components/admin/FormsView.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))
const router = createRouter({ history: createMemoryHistory(), routes })
const mountView = (extra: { attachTo?: Element } = {}) =>
  mountComponent(FormsView, { ...extra, global: { plugins: [router] } })

const aForm = (overrides: Record<string, unknown> = {}) => ({
  id: 'f1',
  title: 'Anmeldung',
  createdAt: '2026-09-22T00:00:00Z',
  fieldCount: 2,
  responseCount: 3,
  formToken: 'tok123456789012345678',
  ...overrides,
})

describe('FormsView', () => {
  beforeEach(() => {
    vi.mocked(listForms).mockReset()
    vi.mocked(listForms).mockResolvedValue([aForm()])
    vi.mocked(createForm).mockReset()
    vi.mocked(deleteForm).mockReset()
    vi.mocked(deleteForm).mockResolvedValue(undefined)
  })

  it('shows each form with its field and response counts', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="form-field-count"]').text()).toContain('2')
    expect(wrapper.get('[data-testid="form-response-count"]').text()).toContain('3')
  })

  it('offers the link once a field exists', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-link"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="form-no-fields"]').exists()).toBe(false)
  })

  it('states that no link is offered while the form has zero fields (010 FR-011)', async () => {
    vi.mocked(listForms).mockResolvedValue([aForm({ fieldCount: 0 })])

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-no-fields"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="form-link"]').exists()).toBe(false)
  })

  it('offers creating directly from the empty state', async () => {
    vi.mocked(listForms).mockResolvedValue([])

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="form-create-empty"]').exists()).toBe(true)
  })

  it('distinguishes "nothing stored" from "cannot be read"', async () => {
    vi.mocked(listForms).mockRejectedValue({ code: 'storage_unavailable' })

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="form-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="form-row"]').exists()).toBe(false)
  })

  it('keeps the creation dialog closed until it is asked for, and it holds only a title', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="form-create-dialog"]').exists()).toBe(false)

    await wrapper.get('[data-testid="form-create-open"]').trigger('click')
    expect(wrapper.find('[data-testid="form-create-dialog"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="form-create-title"]').exists()).toBe(true)

    await wrapper.get('[data-testid="form-create-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="form-create-dialog"]').exists()).toBe(false)
  })

  it('creates a form from just a title and opens the builder', async () => {
    vi.mocked(createForm).mockResolvedValue(
      { ...aForm({ id: 'f2', fieldCount: 0, responseCount: 0 }), fields: [] },
    )
    const pushSpy = vi.spyOn(router, 'push')

    const wrapper = mountView()
    await flush()
    await wrapper.get('[data-testid="form-create-open"]').trigger('click')
    await wrapper.get('[data-testid="form-create-title"] input').setValue('Neues Formular')
    await wrapper.get('[data-testid="form-create-form"]').trigger('submit')
    await flush()

    expect(createForm).toHaveBeenCalledWith('Neues Formular')
    expect(pushSpy).toHaveBeenCalledWith({ name: 'form-builder', params: { formId: 'f2' } })
  })

  it('deletes a form after an explicit confirmation naming its response count', async () => {
    document.body.innerHTML = ''
    const wrapper = mountView({ attachTo: document.body })
    await flush()

    await wrapper.get('[data-testid="form-delete"]').trigger('click')
    await flush()
    expect(document.body.querySelector('[data-testid="form-delete-confirm-body"]')?.textContent)
      .toContain('3')

    document.body.querySelector<HTMLButtonElement>('[data-testid="delete-confirm-button"]')?.click()
    await flush()

    expect(deleteForm).toHaveBeenCalledWith('f1')
    document.body.innerHTML = ''
  })
})
