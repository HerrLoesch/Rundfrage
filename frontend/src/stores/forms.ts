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

  /**
   * Every write funnels through here: one place that clears the previous problem, records the
   * new one, and reports whether the change took.
   */
  async function mutate(change: () => Promise<void>): Promise<boolean> {
    problem.value = null
    try {
      await change()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  /**
   * The caller opens the new form's builder straight away, which loads it - so the list is not
   * re-read here only to be left behind.
   */
  async function create(title: string): Promise<FormDetail | null> {
    let created: FormDetail | null = null
    await mutate(async () => {
      created = await apiCreate(title)
    })
    return created
  }

  function remove(formId: string): Promise<boolean> {
    return mutate(async () => {
      await apiDelete(formId)
      forms.value = forms.value.filter((form) => form.id !== formId)
      if (current.value?.id === formId) current.value = null
    })
  }

  function rename(title: string): Promise<boolean> {
    const id = current.value!.id
    return mutate(async () => {
      current.value = await apiRename(id, title)
    })
  }

  /** The server answers with the new field, appended last - no need to re-read the whole form. */
  function addField(draft: FormFieldDraft): Promise<boolean> {
    const id = current.value!.id
    return mutate(async () => {
      const field = await apiAddField(id, draft)
      const form = current.value!
      current.value = { ...form, fields: [...form.fields, field], fieldCount: form.fieldCount + 1 }
    })
  }

  /** The server answers with the edited field, which replaces its old copy in place. */
  function updateField(fieldId: string, patch: FormFieldPatch): Promise<boolean> {
    const id = current.value!.id
    return mutate(async () => {
      const field = await apiUpdateField(id, fieldId, patch)
      const form = current.value!
      current.value = { ...form, fields: form.fields.map((f) => (f.id === fieldId ? field : f)) }
    })
  }

  /**
   * Mirrors what the server just did (010 FR-009): the field goes, the rest close up so their
   * order stays dense, and every value collected for it leaves the responses with it.
   */
  function removeField(fieldId: string): Promise<boolean> {
    const id = current.value!.id
    return mutate(async () => {
      await apiRemoveField(id, fieldId)
      const form = current.value!
      const fields = form.fields
        .filter((f) => f.id !== fieldId)
        .map((f, index) => ({ ...f, displayOrder: index }))
      current.value = { ...form, fields, fieldCount: fields.length }
      responses.value = responses.value.map((r) => ({
        ...r,
        values: r.values.filter((v) => v.fieldId !== fieldId),
      }))
    })
  }

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

    const ok = await mutate(async () => {
      current.value = await apiReorder(form.id, fieldIds)
    })
    if (!ok) current.value = { ...form, fields: previousFields }
    return ok
  }

  function deleteResponse(responseId: string): Promise<boolean> {
    const id = current.value!.id
    return mutate(async () => {
      await apiDeleteResponse(id, responseId)
      responses.value = responses.value.filter((r) => r.id !== responseId)
      current.value = { ...current.value!, responseCount: current.value!.responseCount - 1 }
    })
  }

  return {
    forms, current, responses, loading, loadProblem, problem,
    load, open, create, remove, rename, addField, updateField, removeField,
    reorderFields, deleteResponse,
  }
})
