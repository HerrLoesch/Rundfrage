import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  addFormField as apiAddField,
  createForm as apiCreate,
  deleteForm as apiDelete,
  deleteFormResponse as apiDeleteResponse,
  fetchForm,
  listForms,
  listFormResponses,
  removeFormField as apiRemoveField,
  renameForm as apiRename,
  reorderFormFields as apiReorder,
  updateFormField as apiUpdateField,
  type ApiProblem,
  type FormDetail,
  type FormFieldDraft,
  type FormFieldPatch,
  type FormResponseSummary,
  type FormSummary,
} from '../api/client'

/**
 * The operator's side of forms: the list, one open form's fields, and its responses.
 *
 * Two problem fields, for the reason the wish-list store records: reading and writing fail for
 * unrelated reasons, and the interface says different things about them.
 */
export const useFormsStore = defineStore('forms', () => {
  const forms = ref<FormSummary[]>([])
  const current = ref<FormDetail | null>(null)
  const responses = ref<FormResponseSummary[]>([])
  const loading = ref(false)

  const loadProblem = ref<ApiProblem | null>(null)
  const problem = ref<ApiProblem | null>(null)

  async function load(): Promise<void> {
    loading.value = true
    loadProblem.value = null
    try {
      forms.value = await listForms()
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
    } finally {
      loading.value = false
    }
  }

  async function open(formId: string): Promise<void> {
    loading.value = true
    loadProblem.value = null
    current.value = null
    responses.value = []
    try {
      current.value = await fetchForm(formId)
      responses.value = await listFormResponses(formId)
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
    } finally {
      loading.value = false
    }
  }

  async function create(title: string): Promise<FormDetail | null> {
    problem.value = null
    try {
      const created = await apiCreate(title)
      await load()
      return created
    } catch (failure) {
      problem.value = failure as ApiProblem
      return null
    }
  }

  async function remove(formId: string): Promise<boolean> {
    problem.value = null
    try {
      await apiDelete(formId)
      forms.value = forms.value.filter((form) => form.id !== formId)
      if (current.value?.id === formId) current.value = null
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  /** Every field mutation funnels through here: re-read from the server's own account of it. */
  async function apply(change: () => Promise<FormDetail>): Promise<boolean> {
    problem.value = null
    try {
      current.value = await change()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  function rename(title: string) {
    return apply(() => apiRename(current.value!.id, title))
  }

  async function addField(draft: FormFieldDraft): Promise<boolean> {
    const id = current.value!.id
    problem.value = null
    try {
      await apiAddField(id, draft)
      current.value = await fetchForm(id)
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  async function updateField(fieldId: string, patch: FormFieldPatch): Promise<boolean> {
    const id = current.value!.id
    problem.value = null
    try {
      await apiUpdateField(id, fieldId, patch)
      current.value = await fetchForm(id)
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  async function removeField(fieldId: string): Promise<boolean> {
    const id = current.value!.id
    return apply(async () => {
      await apiRemoveField(id, fieldId)
      return fetchForm(id)
    })
  }

  /**
   * FR-007/SC-007: the new order is shown immediately, before the request resolves - a dragged
   * or button-reordered field must not wait on a round trip to appear where it was dropped. The
   * previous order is restored if the request is refused.
   */
  /**
   * FR-007/SC-007: the new order is shown immediately, before the request resolves - a dragged
   * or button-reordered field must not wait on a round trip to appear where it was dropped. The
   * previous order is restored if the request is refused.
   */
  async function reorderFields(fieldIds: string[]): Promise<boolean> {
    const form = current.value
    if (!form) return false

    const previousFields = form.fields
    const byId = new Map(previousFields.map((field) => [field.id, field]))
    const reordered = fieldIds.map((id, index) => ({ ...byId.get(id)!, displayOrder: index }))

    current.value = { ...form, fields: reordered }
    problem.value = null

    try {
      current.value = await apiReorder(form.id, fieldIds)
      return true
    } catch (failure) {
      current.value = { ...form, fields: previousFields }
      problem.value = failure as ApiProblem
      return false
    }
  }

  async function deleteResponse(responseId: string): Promise<boolean> {
    const id = current.value!.id
    problem.value = null
    try {
      await apiDeleteResponse(id, responseId)
      responses.value = responses.value.filter((r) => r.id !== responseId)
      current.value = await fetchForm(id)
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  return {
    forms, current, responses, loading, loadProblem, problem,
    load, open, create, remove, rename, addField, updateField, removeField,
    reorderFields, deleteResponse,
  }
})
