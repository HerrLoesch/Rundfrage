<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  previewRestore,
  restoreBackup,
  type ApiProblem,
  type RestorePreview,
  type RestoreSummary,
} from '../../api/client'
import { useMaintenanceStore } from '../../stores/maintenance'

const emit = defineEmits<{ restored: [] }>()

const { t } = useI18n()
const maintenance = useMaintenanceStore()

const file = ref<File | null>(null)
const busy = ref(false)
const preview = ref<RestorePreview | null>(null)
const summary = ref<RestoreSummary | null>(null)
const error = ref<string | null>(null)

/**
 * FR-024 made visible. The control is inert without maintenance mode, and says which step is
 * missing rather than leaving the operator to work it out - a disabled control with no reason is
 * a puzzle, not a guard.
 */
const blocked = computed(() => !maintenance.enabled)

function chosen(files: File | File[] | null) {
  file.value = Array.isArray(files) ? (files[0] ?? null) : files
  preview.value = null
  summary.value = null
  error.value = null
}

async function check() {
  if (!file.value || busy.value || blocked.value) return

  busy.value = true
  error.value = null
  summary.value = null

  try {
    preview.value = await previewRestore(file.value)
  } catch (problem) {
    const { code } = problem as ApiProblem
    error.value = t(`error.${code}`)
  } finally {
    busy.value = false
  }
}

/** Only reachable after a preview: the confirmation confirms the loss it names (FR-018). */
async function confirm() {
  if (!file.value || busy.value || !preview.value) return

  busy.value = true
  error.value = null

  try {
    summary.value = await restoreBackup(file.value)
    preview.value = null
    emit('restored')
  } catch (problem) {
    const { code } = problem as ApiProblem
    error.value = t(`error.${code}`)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <!--
    Visually separated from the JSON import and marked as affecting everything. FR-001: adding one
    poll and replacing all of them must never be reachable by the same reflex.

    No card and no title of its own. SettingsView already gives this a bordered, error-coloured
    section headed "Sicherung wiederherstellen"; repeating both produced a red box inside an
    orange box inside a red box, and the same heading twice forty pixels apart. A panel that is
    always placed inside a titled section must not bring a second title.
  -->
  <div data-testid="restore-panel">
    <v-alert type="warning" density="compact" class="mb-4">{{ t('restore.warning') }}</v-alert>

    <v-alert
      v-if="blocked"
      type="info"
      density="compact"
      class="mb-4"
      data-testid="restore-blocked"
    >
      {{ t('restore.needsMaintenance') }}
    </v-alert>

    <!-- The field and the action that reads it belong on one line: choosing a file and checking
         it are one task, and the button was previously stranded a full row below the input. -->
    <div class="rf-field-row">
      <v-file-input
        class="rf-field-row__grow"
        accept=".db,application/octet-stream"
        :label="t('restore.choose')"
        :disabled="blocked || busy"
        prepend-icon="mdi-database-arrow-up-outline"
        data-testid="restore-file"
        @update:model-value="chosen"
      />

      <v-btn
        variant="outlined"
        size="large"
        :disabled="blocked || !file || busy"
        :loading="busy && !preview"
        data-testid="restore-check"
        @click="check"
      >
        {{ busy && !preview ? t('restore.checking') : t('restore.check') }}
      </v-btn>
    </div>

    <!-- FR-018: what it holds, and what will be lost, before anything is replaced. -->
    <v-card v-if="preview" class="mt-4" data-testid="restore-preview">
      <v-card-item>
        <v-card-title tag="h3" class="text-subtitle-2">
          {{ t('restore.previewTitle') }}
        </v-card-title>
      </v-card-item>

      <v-card-text class="text-body-2">
        <p class="mb-1">
          {{
            t('restore.previewContains', {
              polls: preview.pollsInBackup,
              responses: preview.responsesInBackup,
            })
          }}
        </p>
        <!--
          Feature 008: wish lists are replaced too, so they are named too. Saying only what a
          restore does to polls would make the confirmation understate the loss (008 R-10).
        -->
        <p class="mb-1" data-testid="restore-preview-wish-lists">
          {{
            t('restore.previewContainsWishLists', {
              count: preview.wishListsInBackup,
              claims: preview.claimsInBackup,
            })
          }}
        </p>
        <p
          v-if="preview.wishListsLost > 0 || preview.claimsLost > 0"
          class="mb-1 font-weight-bold"
          data-testid="restore-preview-wish-lists-lost"
        >
          {{
            t('restore.previewLosesWishLists', {
              count: preview.wishListsLost,
              claims: preview.claimsLost,
            })
          }}
        </p>
        <p v-if="preview.pollsLost > 0 || preview.responsesLost > 0" class="mb-1 font-weight-bold">
          {{
            t('restore.previewLoses', {
              polls: preview.pollsLost,
              responses: preview.responsesLost,
            })
          }}
        </p>
        <p v-else-if="preview.wishListsLost === 0 && preview.claimsLost === 0" class="mb-1">
          {{ t('restore.previewLosesNothing') }}
        </p>
        <p v-if="preview.expired.length > 0" class="mb-0">
          {{ t('restore.previewExpired', { titles: preview.expired.join(', ') }) }}
        </p>
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" data-testid="restore-cancel" @click="preview = null">
          {{ t('restore.cancel') }}
        </v-btn>
        <!--
          The button names the loss. A bare "OK" here would be the last thing between a full
          data set and an older one (ui-contract §3b).
        -->
        <v-btn
          color="warning"
          :loading="busy"
          data-testid="restore-confirm"
          @click="confirm"
        >
          {{ t('restore.confirm', { polls: preview.pollsLost }) }}
        </v-btn>
      </v-card-actions>
    </v-card>

    <v-alert v-if="error" type="error" class="mt-4" data-testid="restore-error">
      {{ error }}
    </v-alert>

    <v-alert v-if="summary" type="success" class="mt-4" data-testid="restore-done">
      <div class="text-subtitle-2 font-weight-bold">{{ t('restore.doneTitle') }}</div>
      {{ t('restore.done', { polls: summary.polls, responses: summary.responses }) }}

      <!--
        FR-016a. A restore reproduces the backup rather than filtering it, so polls already past
        their retention date come back and are then removed by the next sweep. Saying so here is
        what turns that from a surprise into an announcement - the preview alone is not enough,
        because it is gone by the time the restore has finished.
      -->
      <p
        v-if="summary.expired.length > 0"
        class="text-body-2 mt-2 mb-0"
        data-testid="restore-done-expired"
      >
        {{ t('restore.doneExpired', { titles: summary.expired.join(', ') }) }}
      </p>
    </v-alert>
  </div>
</template>
