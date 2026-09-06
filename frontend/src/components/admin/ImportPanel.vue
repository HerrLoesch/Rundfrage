<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { importPoll, type ApiProblem, type ImportSummary as Summary } from '../../api/client'
import ImportSummary from './ImportSummary.vue'

const emit = defineEmits<{ imported: [] }>()

const { t } = useI18n()

const file = ref<File | null>(null)
const busy = ref(false)
const summary = ref<Summary | null>(null)
const error = ref<string | null>(null)

/**
 * No confirmation step. An import is additive - it cannot modify or replace an existing poll
 * (FR-008) - so there is nothing to warn about. The destructive operation is a restore, and it
 * lives in its own panel with its own confirmation for exactly that reason (FR-001).
 */
async function submit() {
  if (!file.value || busy.value) return

  busy.value = true
  error.value = null
  summary.value = null

  try {
    summary.value = await importPoll(file.value)
    emit('imported')
  } catch (problem) {
    const { code, limit } = problem as ApiProblem
    error.value = t(`error.${code}`, { limit: limit ?? 0 })
  } finally {
    busy.value = false
  }
}

function chosen(files: File | File[] | null) {
  file.value = Array.isArray(files) ? (files[0] ?? null) : files
  summary.value = null
  error.value = null
}
</script>

<template>
  <v-card data-testid="import-panel">
    <v-card-title class="text-subtitle-1">{{ t('import.title') }}</v-card-title>

    <v-card-text>
      <p class="text-body-2 mb-3">{{ t('import.hint') }}</p>

      <v-file-input
        accept="application/json,.json"
        :label="t('import.choose')"
        :disabled="busy"
        prepend-icon="mdi-file-document-outline"
        data-testid="import-file"
        @update:model-value="chosen"
      />

      <v-btn
        color="primary"
        :disabled="!file || busy"
        :loading="busy"
        data-testid="import-submit"
        @click="submit"
      >
        {{ busy ? t('import.running') : t('import.submit') }}
      </v-btn>

      <v-alert v-if="error" type="error" class="mt-4" data-testid="import-error">
        {{ error }}
      </v-alert>

      <ImportSummary v-if="summary" :summary="summary" />
    </v-card-text>
  </v-card>
</template>
