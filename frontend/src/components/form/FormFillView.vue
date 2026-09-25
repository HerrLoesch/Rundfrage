<script setup lang="ts">
import { computed, onMounted, reactive, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useFormFillStore } from '../../stores/formFill'
import { isAnswered, isValidValue } from '../../composables/useFormFieldValidation'
import type { FormFieldDetail } from '../../api/client'

const props = defineProps<{ formToken: string }>()

const { t } = useI18n()
const store = useFormFillStore()

/** fieldId -> the value as entered so far. Absent/null means "not yet answered" (research R-13). */
const answers = reactive<Record<string, string | null>>({})

/** fieldId -> the client-side error currently shown, or null (010 FR-016). */
const errors = reactive<Record<string, 'required' | 'invalid_format' | null>>({})

onMounted(() => store.load(props.formToken))

watch(
  () => store.definition,
  (definition) => {
    if (!definition) return
    for (const field of definition.fields) {
      answers[field.id] = null
      errors[field.id] = null
    }
  },
)

/** 010 FR-021: an unknown, malformed, zero-field or deleted form all look the same. */
const unavailable = computed(
  () => store.loadProblem !== null && store.loadProblem.code !== 'maintenance',
)
const maintenance = computed(() => store.loadProblem?.code === 'maintenance')

function fieldError(field: FormFieldDetail): 'required' | 'invalid_format' | null {
  const value = answers[field.id]

  if (!isAnswered(value)) {
    return field.required ? 'required' : null
  }

  return isValidValue(field, value) ? null : 'invalid_format'
}

/** 010 FR-016: blocks submission client-side and identifies every field at fault. */
function validate(): boolean {
  if (!store.definition) return false

  let firstInvalid: string | null = null

  for (const field of store.definition.fields) {
    const error = fieldError(field)
    errors[field.id] = error
    if (error && firstInvalid === null) firstInvalid = field.id
  }

  if (firstInvalid) {
    document
      .querySelector<HTMLElement>(`[data-field-id="${firstInvalid}"] input, [data-field-id="${firstInvalid}"] textarea`)
      ?.focus()
    return false
  }

  return true
}

async function submit() {
  if (!validate() || !store.definition) return

  const values = store.definition.fields
    .filter((field) => isAnswered(answers[field.id]))
    .map((field) => ({ fieldId: field.id, value: answers[field.id]! }))

  const ok = await store.submit(values)

  if (!ok && store.submitProblem?.fields) {
    // The server's error codes carry the "field_" prefix that ErrorCodes.cs defines
    // (e.g. "field_required"); `errors` stores only the bare suffix the template's
    // `error.field_${...}` lookup re-adds, matching the shape client-side errors already use.
    // This case is reachable in ordinary use, not just from a bypassing client: the operator can
    // add a new required field while a participant already has the form open, so the server
    // rejects something the participant's stale, already-loaded field list never validated.
    for (const fieldError of store.submitProblem.fields) {
      errors[fieldError.fieldId] = fieldError.error.replace(/^field_/, '') as
        | 'required'
        | 'invalid_format'
    }
  }
}

function setBoolean(fieldId: string, value: 'yes' | 'no') {
  answers[fieldId] = value
}

/**
 * A message specific to what is actually wrong with this field, rather than the one generic
 * "does not match the expected format" every type shared before - a bad e-mail address, a bad
 * postal code and a bad phone number are different mistakes and read as one only because the
 * text describing them was identical. The three cases 'error.field_required' already covers
 * generically; every 'invalid_format' is resolved to the one violation that field's type can
 * actually have.
 */
function fieldErrorMessage(field: FormFieldDetail, error: 'required' | 'invalid_format'): string {
  if (error === 'required') return t('error.field_required')

  switch (field.type) {
    case 'email':
      return t('error.field_invalid_email')
    case 'phone':
      return t('error.field_invalid_phone')
    case 'postalCode':
      return t('error.field_invalid_postal_code')
    case 'integer':
      return t('error.field_invalid_integer')
    case 'decimal':
      return t('error.field_invalid_decimal')
    case 'boolean':
      // Unreachable through the radio group itself - kept as a defensive fallback for a
      // server-side rejection of a field the client never rendered as invalid.
      return t('error.field_invalid_boolean')
    case 'text':
      if (field.minLength != null && field.maxLength != null) {
        return t('error.field_invalid_text_range', { min: field.minLength, max: field.maxLength })
      }
      if (field.minLength != null) {
        return t('error.field_invalid_text_min', { min: field.minLength })
      }
      return t('error.field_invalid_text_max', { max: field.maxLength ?? 0 })
  }
}
</script>

<template>
  <v-container class="rf-page" data-testid="form-fill-view">
    <v-alert v-if="maintenance" type="info" data-testid="form-fill-maintenance">
      {{ t('maintenance.noticeBody') }}
    </v-alert>

    <v-alert v-else-if="unavailable" type="info" data-testid="form-fill-unavailable">
      {{ t('form.fill.notFound') }}
    </v-alert>

    <div v-else-if="store.loading" class="text-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else-if="store.submitted">
      <v-alert type="success" data-testid="form-fill-confirmation">
        {{ t('form.fill.submitted') }}
      </v-alert>
    </template>

    <v-form v-else-if="store.definition" @submit.prevent="submit">
      <h1 class="text-h4 mb-4" data-testid="form-fill-title">{{ store.definition.title }}</h1>

      <div
        v-for="field in store.definition.fields"
        :key="field.id"
        class="mb-4"
        data-testid="form-fill-field"
        :data-field-id="field.id"
      >
        <label class="d-block mb-1">
          {{ field.label }}
          <span v-if="field.required" data-testid="form-fill-required-marker" :aria-label="t('form.fill.required')">
            {{ t('form.fill.required') }}
          </span>
        </label>

        <!-- Boolean: a radio group with nothing pre-selected, never a checkbox (research R-13). -->
        <v-radio-group
          v-if="field.type === 'boolean'"
          :model-value="answers[field.id]"
          inline
          hide-details
          @update:model-value="(v) => v && setBoolean(field.id, v as 'yes' | 'no')"
        >
          <v-radio :label="t('form.fill.yes')" value="yes" />
          <v-radio :label="t('form.fill.no')" value="no" />
        </v-radio-group>

        <v-text-field
          v-else
          v-model="answers[field.id]"
          :type="field.type === 'email' ? 'email' : field.type === 'phone' ? 'tel' : 'text'"
          :inputmode="field.type === 'integer' || field.type === 'decimal' || field.type === 'postalCode' ? 'numeric' : 'text'"
          :maxlength="field.type === 'text' ? (field.maxLength ?? undefined) : undefined"
          :counter="field.type === 'text' ? (field.maxLength ?? undefined) : undefined"
          hide-details
        />

        <div
          v-if="errors[field.id]"
          class="text-error text-body-2 mt-1"
          data-testid="form-fill-field-error"
        >
          {{ fieldErrorMessage(field, errors[field.id]!) }}
        </div>
      </div>

      <v-btn color="primary" type="submit" data-testid="form-fill-submit">
        {{ t('form.fill.submit') }}
      </v-btn>
    </v-form>
  </v-container>
</template>
