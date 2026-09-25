import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  fetchFormByToken,
  submitFormResponse,
  type ApiProblem,
  type FormFieldSubmissionValue,
  type FormSubmissionProblem,
  type PublicFormDefinition,
} from '../api/client'

/**
 * One participant's in-progress answers to one form (010 FR-012 to FR-021).
 *
 * Kept separate from `forms.ts`, for the reason `creator.ts` is kept separate from `creators.ts`:
 * that store talks to `/admin/forms/**` and belongs to the operator; this one talks to
 * `/f/{token}` and belongs to whoever holds the link. Sharing a store would be sharing a URL
 * prefix, and the first mistake would be a participant request reaching an admin endpoint.
 */
export const useFormFillStore = defineStore('formFill', () => {
  const token = ref('')
  const definition = ref<PublicFormDefinition | null>(null)
  const loading = ref(false)
  const loadProblem = ref<ApiProblem | null>(null)

  const submitting = ref(false)
  const submitted = ref(false)
  const submitProblem = ref<FormSubmissionProblem | null>(null)

  async function load(formToken: string): Promise<void> {
    token.value = formToken
    loading.value = true
    loadProblem.value = null
    definition.value = null
    submitted.value = false
    try {
      definition.value = await fetchFormByToken(formToken)
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
    } finally {
      loading.value = false
    }
  }

  async function submit(
    values: { fieldId: string; value: FormFieldSubmissionValue }[],
  ): Promise<boolean> {
    submitting.value = true
    submitProblem.value = null
    try {
      await submitFormResponse(token.value, values)
      submitted.value = true
      return true
    } catch (failure) {
      submitProblem.value = failure as FormSubmissionProblem
      return false
    } finally {
      submitting.value = false
    }
  }

  return {
    token, definition, loading, loadProblem, submitting, submitted, submitProblem,
    load, submit,
  }
})
