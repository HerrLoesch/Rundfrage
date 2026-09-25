<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useFormsStore } from '../../stores/forms'
import { useSessionStore } from '../../stores/session'
import { useProblemText } from '../../composables/useProblemText'
import PageHeader from '../layout/PageHeader.vue'
import DeleteConfirm from './DeleteConfirm.vue'
import {
  formExportCsvUrl,
  formExportJsonUrl,
  type FieldType,
  type FormFieldDetail,
  type FormResponseSummary,
} from '../../api/client'

const props = defineProps<{ formId: string }>()

const { t, d } = useI18n()
const router = useRouter()
const store = useFormsStore()
const session = useSessionStore()
const problemText = useProblemText()

const title = ref('')
const removingField = ref<FormFieldDetail | null>(null)
const removingResponse = ref<FormResponseSummary | null>(null)

/** All seven types, in the order they are offered when adding a field (010 FR-003). */
const fieldTypes: FieldType[] = [
  'text', 'integer', 'decimal', 'boolean', 'email', 'phone', 'postalCode',
]

function fieldTypeLabel(type: FieldType): string {
  return t(`form.fieldType${type.charAt(0).toUpperCase()}${type.slice(1)}`)
}

const addFieldOpen = ref(false)
const draftType = ref<FieldType>('text')
const draftLabel = ref('')
const draftRequired = ref(false)
const draftMaxLength = ref<number | null>(100)
const draftMinLength = ref<number | null>(null)

/**
 * Its own address, so this page can be linked to and survives a reload, following 007's ordinary
 * rule - unlike the creator surface, there is no credential in this address to protect.
 */
onMounted(async () => {
  await store.open(props.formId)

  if (store.loadProblem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
    return
  }

  if (store.loadProblem?.code === 'not_found') {
    await router.push({ name: 'forms' })
  }
})

watch(
  () => store.current,
  (form) => {
    if (form) title.value = form.title
  },
  { immediate: true },
)

const storageUnavailable = computed(
  () => store.loadProblem !== null && store.loadProblem.code !== 'unauthorized',
)

const fields = computed(() => store.current?.fields ?? [])

function share(formToken: string): string {
  return `${window.location.origin}/f/${formToken}`
}

async function saveTitle() {
  if (!store.current || title.value.trim() === store.current.title) return
  await store.rename(title.value)
}

function openAddField() {
  draftType.value = 'text'
  draftLabel.value = ''
  draftRequired.value = false
  draftMaxLength.value = 100
  draftMinLength.value = null
  addFieldOpen.value = true
}

async function submitAddField() {
  const ok = await store.addField({
    type: draftType.value,
    label: draftLabel.value,
    required: draftRequired.value,
    maxLength: draftType.value === 'text' ? draftMaxLength.value : null,
    minLength: draftType.value === 'text' ? draftMinLength.value : null,
  })
  if (ok) addFieldOpen.value = false
}

async function toggleRequired(field: FormFieldDetail) {
  await store.updateField(field.id, { required: !field.required })
}

async function saveLabel(field: FormFieldDetail, newLabel: string) {
  if (newLabel.trim() === '' || newLabel === field.label) return
  await store.updateField(field.id, { label: newLabel })
}

async function saveLengths(field: FormFieldDetail, maxLength: number | null, minLength: number | null) {
  await store.updateField(field.id, { maxLength, minLength })
}

/**
 * Parses a length input, treating an emptied field as "leave the current value" rather than as
 * zero - `Number(raw) || fallback` would do that already for every other blank input, but it
 * also silently discards a deliberately typed "0" (0 is falsy), turning a would-be edit into a
 * no-op with nothing telling the operator it did not take effect.
 */
function parseLength(raw: string, fallback: number | null): number | null {
  if (raw === '') return fallback
  const parsed = Number(raw)
  return Number.isNaN(parsed) ? fallback : parsed
}

async function confirmRemoveField() {
  const field = removingField.value
  if (!field) return
  await store.removeField(field.id)
  removingField.value = null
}

async function moveField(index: number, direction: -1 | 1) {
  const ordered = [...fields.value]
  const target = index + direction
  if (target < 0 || target >= ordered.length) return

  const [moved] = ordered.splice(index, 1)
  ordered.splice(target, 0, moved)
  await store.reorderFields(ordered.map((f) => f.id))
}

const dragIndex = ref<number | null>(null)

function onDragStart(index: number) {
  dragIndex.value = index
}

async function onDrop(targetIndex: number) {
  if (dragIndex.value === null || dragIndex.value === targetIndex) return

  const ordered = [...fields.value]
  const [moved] = ordered.splice(dragIndex.value, 1)
  ordered.splice(targetIndex, 0, moved)
  dragIndex.value = null
  await store.reorderFields(ordered.map((f) => f.id))
}

function displayValue(fieldId: string, value: string): string {
  const field = fields.value.find((f) => f.id === fieldId)
  if (field?.type === 'boolean') {
    return value === 'yes' ? t('form.fill.yes') : t('form.fill.no')
  }
  return value
}

/**
 * A response's value for one column, or empty - a field added after the response was submitted
 * has no row for it at all, which the table renders as a blank cell rather than a guessed
 * default (010 FR-038).
 */
function tableCell(response: FormResponseSummary, fieldId: string): string {
  const value = response.values.find((v) => v.fieldId === fieldId)?.value
  return value === undefined ? '' : displayValue(fieldId, value)
}

/**
 * How many already-collected responses hold a value for this field - what the removal
 * confirmation states before the cascade delete destroys them (010 FR-009, ui-contract.md §4).
 */
function collectedValueCount(fieldId: string): number {
  return store.responses.filter((r) => r.values.some((v) => v.fieldId === fieldId)).length
}

async function confirmRemoveResponse() {
  const response = removingResponse.value
  if (!response) return
  await store.deleteResponse(response.id)
  removingResponse.value = null
}
</script>

<template>
  <v-container class="rf-page rf-page--admin">
    <PageHeader :title="store.current?.title ?? ''" title-testid="form-builder-title">
      <template #actions>
        <v-btn variant="text" prepend-icon="mdi-arrow-left" @click="router.push({ name: 'forms' })">
          {{ t('form.backToList') }}
        </v-btn>
      </template>
    </PageHeader>

    <v-alert v-if="storageUnavailable" type="warning" class="mb-4" data-testid="form-builder-unavailable">
      {{ t('storage.unavailable') }}
    </v-alert>

    <template v-else-if="store.current">
      <v-text-field
        v-model="title"
        :label="t('form.rename')"
        class="mb-4"
        data-testid="form-builder-title-input"
        @blur="saveTitle"
        @keydown.enter="saveTitle"
      />

      <!-- Renaming, editing a label, toggling required, changing a length limit, reordering and
           deleting a response all report failures here - the one place on this page (besides the
           add-field dialog itself) that a rejected edit is not simply silent. -->
      <v-alert v-if="store.problem" type="error" class="mb-4" data-testid="form-builder-error">
        {{ problemText(store.problem) }}
      </v-alert>

      <span v-if="fields.length === 0" class="rf-meta text-medium-emphasis mb-4 d-block" data-testid="form-builder-no-link">
        {{ t('form.noFields') }}
      </span>
      <a
        v-else
        class="rf-address mb-4 d-block"
        :href="share(store.current.formToken)"
        target="_blank"
        rel="noopener noreferrer"
        data-testid="form-builder-link"
        >{{ share(store.current.formToken) }}</a
      >

      <h2 class="text-h6 mb-2">{{ t('form.addField') }}</h2>

      <div data-testid="form-field-canvas">
        <v-alert v-if="fields.length === 0" type="info" class="mb-4" data-testid="form-field-canvas-empty">
          {{ t('form.fieldCanvasEmpty') }}
        </v-alert>

        <v-card
          v-for="(field, index) in fields"
          :key="field.id"
          class="rf-row mb-3"
          data-testid="form-field-card"
          draggable="true"
          @dragstart="onDragStart(index)"
          @dragover.prevent
          @drop="onDrop(index)"
        >
          <v-card-text class="d-flex flex-wrap align-center ga-3">
            <v-icon
              icon="mdi-drag"
              data-testid="form-field-drag-handle"
              :aria-label="t('form.dragHandle')"
              tabindex="0"
            />

            <v-chip data-testid="form-field-type">{{ fieldTypeLabel(field.type) }}</v-chip>

            <v-text-field
              :model-value="field.label"
              :label="t('form.fieldLabel')"
              data-testid="form-field-label-input"
              hide-details
              style="max-width: 240px"
              @blur="(e: FocusEvent) => saveLabel(field, (e.target as HTMLInputElement).value)"
            />

            <v-switch
              :model-value="field.required"
              :label="field.required ? t('form.fieldRequired') : t('form.fieldOptional')"
              data-testid="form-field-required-toggle"
              hide-details
              @update:model-value="() => toggleRequired(field)"
            />

            <template v-if="field.type === 'text'">
              <v-text-field
                :model-value="field.maxLength"
                type="number"
                :label="t('form.fieldMaxLength')"
                data-testid="form-field-max-length-input"
                hide-details
                style="max-width: 140px"
                @blur="(e: FocusEvent) => saveLengths(field, parseLength((e.target as HTMLInputElement).value, field.maxLength), field.minLength)"
              />
              <v-text-field
                :model-value="field.minLength"
                type="number"
                :label="t('form.fieldMinLength')"
                data-testid="form-field-min-length-input"
                hide-details
                style="max-width: 140px"
                @blur="(e: FocusEvent) => saveLengths(field, field.maxLength, (e.target as HTMLInputElement).value === '' ? null : Number((e.target as HTMLInputElement).value))"
              />
            </template>

            <v-spacer />

            <v-btn
              icon="mdi-arrow-up"
              variant="text"
              :disabled="index === 0"
              :aria-label="t('form.moveFieldUp')"
              data-testid="form-field-move-up"
              @click="moveField(index, -1)"
            />
            <v-btn
              icon="mdi-arrow-down"
              variant="text"
              :disabled="index === fields.length - 1"
              :aria-label="t('form.moveFieldDown')"
              data-testid="form-field-move-down"
              @click="moveField(index, 1)"
            />
            <v-btn
              icon="mdi-delete-outline"
              variant="text"
              color="error"
              :aria-label="t('form.removeField')"
              data-testid="form-field-remove"
              @click="removingField = field"
            />
          </v-card-text>
        </v-card>
      </div>

      <v-btn
        color="primary"
        prepend-icon="mdi-plus"
        class="mt-2"
        data-testid="form-add-field"
        @click="openAddField"
      >
        {{ t('form.addField') }}
      </v-btn>

      <v-divider class="my-6" />

      <h2 class="text-h6 mb-2">{{ t('form.responsesTitle') }}</h2>

      <div class="mb-4">
        <v-btn
          variant="tonal"
          prepend-icon="mdi-file-delimited-outline"
          class="mr-2"
          data-testid="form-export-csv"
          :href="formExportCsvUrl(store.current.id)"
        >
          {{ t('form.exportCsv') }}
        </v-btn>
        <v-btn
          variant="tonal"
          prepend-icon="mdi-code-json"
          data-testid="form-export-json"
          :href="formExportJsonUrl(store.current.id)"
        >
          {{ t('form.exportJson') }}
        </v-btn>
      </div>

      <v-alert v-if="store.responses.length === 0" type="info" data-testid="form-responses-empty">
        {{ t('form.responsesEmpty') }}
      </v-alert>

      <!-- One column per field, in the builder's current order (010 FR-010) - the same shape the
           CSV and JSON exports use, so the admin overview shows what the download will contain
           rather than a second, differently-arranged rendering of the same data. -->
      <v-card v-else data-testid="form-responses">
        <v-table density="comfortable" class="scroller">
          <thead>
            <tr>
              <th scope="col">{{ t('form.responsesSubmittedAt') }}</th>
              <th v-for="responseField in fields" :key="responseField.id" scope="col">
                {{ responseField.label }}
              </th>
              <th scope="col" class="text-center">
                <span class="d-sr-only">{{ t('form.deleteResponse') }}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="response in store.responses" :key="response.id" data-testid="form-response-row">
              <td>{{ d(new Date(response.submittedAt), 'numeric') }}</td>
              <td v-for="responseField in fields" :key="responseField.id" data-testid="form-response-cell">
                {{ tableCell(response, responseField.id) }}
              </td>
              <td class="text-center">
                <v-btn
                  icon="mdi-delete-outline"
                  size="small"
                  variant="text"
                  color="error"
                  :aria-label="t('form.deleteResponse')"
                  data-testid="form-response-delete"
                  @click="removingResponse = response"
                />
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card>
    </template>

    <!-- Add-field dialog, offering all seven types (010 FR-003). -->
    <v-dialog v-model="addFieldOpen" max-width="480">
      <v-card data-testid="form-add-field-dialog">
        <v-card-title>{{ t('form.addField') }}</v-card-title>
        <v-card-text>
          <v-select
            v-model="draftType"
            :items="fieldTypes"
            :item-title="fieldTypeLabel"
            :item-value="(item: FieldType) => item"
            :label="t('form.fieldType')"
            data-testid="form-add-field-type"
            class="mb-4"
          />
          <v-text-field
            v-model="draftLabel"
            :label="t('form.fieldLabel')"
            data-testid="form-add-field-label"
            class="mb-4"
          />
          <v-switch
            v-model="draftRequired"
            :label="draftRequired ? t('form.fieldRequired') : t('form.fieldOptional')"
            data-testid="form-add-field-required"
            class="mb-2"
            hide-details
          />
          <template v-if="draftType === 'text'">
            <v-text-field
              v-model.number="draftMaxLength"
              type="number"
              :label="t('form.fieldMaxLength')"
              data-testid="form-add-field-max-length"
              class="mb-4"
            />
            <v-text-field
              v-model.number="draftMinLength"
              type="number"
              :label="t('form.fieldMinLength')"
              data-testid="form-add-field-min-length"
              class="mb-2"
            />
          </template>
          <v-alert v-if="store.problem" type="error" density="compact" data-testid="form-add-field-error">
            {{ problemText(store.problem) }}
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" data-testid="form-add-field-cancel" @click="addFieldOpen = false">
            {{ t('form.cancel') }}
          </v-btn>
          <v-btn color="primary" data-testid="form-add-field-submit" @click="submitAddField">
            {{ t('form.submit') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <DeleteConfirm
      v-if="removingField"
      :title="removingField.label"
      :body="t('form.removeFieldConfirm', { label: removingField.label, count: collectedValueCount(removingField.id) })"
      testid="form-field-remove-confirm"
      @confirm="confirmRemoveField"
      @cancel="removingField = null"
    />

    <DeleteConfirm
      v-if="removingResponse"
      :title="t('form.deleteResponse')"
      :body="t('form.deleteResponseConfirm')"
      testid="form-response-delete-confirm"
      @confirm="confirmRemoveResponse"
      @cancel="removingResponse = null"
    />
  </v-container>
</template>

<style scoped>
.rf-row + .rf-row { margin-block-start: var(--rf-stack); }

/* A form can hold up to 50 columns (010 FR-011a); the table scrolls inside itself rather than
   the page, matching ResultGrid.vue's own answer to the same problem for a poll's day columns. */
.scroller { overflow-x: auto; }
</style>
