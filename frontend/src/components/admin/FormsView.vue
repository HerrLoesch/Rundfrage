<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useFormsStore } from '../../stores/forms'
import { useSessionStore } from '../../stores/session'
import { useProblemText } from '../../composables/useProblemText'
import PageHeader from '../layout/PageHeader.vue'
import DeleteConfirm from './DeleteConfirm.vue'
import type { FormSummary } from '../../api/client'

const { t } = useI18n()
const router = useRouter()
const store = useFormsStore()
const session = useSessionStore()
const problemText = useProblemText()

const revealed = ref(false)
const newTitle = ref('')
const deleting = ref<FormSummary | null>(null)

/** The server is the authority on the session (007 FR-011). */
onMounted(async () => {
  await store.load()

  if (store.loadProblem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
  }
})

/** "Nothing is stored" and "this cannot be read" are different things (010 FR-044). */
const storageUnavailable = computed(
  () => store.loadProblem !== null && store.loadProblem.code !== 'unauthorized',
)

function share(formToken: string): string {
  return `${window.location.origin}/f/${formToken}`
}

function reveal() {
  revealed.value = !revealed.value
  newTitle.value = ''
}

async function submitCreate() {
  const created = await store.create(newTitle.value)
  if (created) {
    revealed.value = false
    await router.push({ name: 'form-builder', params: { formId: created.id } })
  }
}

async function confirmDelete() {
  const form = deleting.value
  if (!form) return
  await store.remove(form.id)
  deleting.value = null
}
</script>

<template>
  <v-container class="rf-page rf-page--admin">
    <PageHeader :title="t('form.listTitle')">
      <template #actions>
        <v-btn
          v-if="!revealed"
          color="primary"
          prepend-icon="mdi-plus"
          data-testid="form-create-open"
          @click="reveal"
        >
          {{ t('form.create') }}
        </v-btn>
      </template>
    </PageHeader>

    <!-- Only the title, unlike the poll/wish-list creation forms - a form's fields are built in
         the canvas, not chosen up front (ui-contract.md §3). -->
    <v-card v-if="revealed" class="rf-section-gap mb-4" data-testid="form-create-dialog">
      <v-card-text>
        <v-form data-testid="form-create-form" @submit.prevent="submitCreate">
          <v-text-field
            v-model="newTitle"
            :label="t('form.title')"
            data-testid="form-create-title"
            autofocus
          />
          <div v-if="store.problem" class="text-error mb-2" data-testid="form-create-error">
            {{ problemText(store.problem) }}
          </div>
          <v-card-actions class="px-0">
            <v-spacer />
            <v-btn variant="text" data-testid="form-create-cancel" @click="revealed = false">
              {{ t('form.cancel') }}
            </v-btn>
            <v-btn color="primary" type="submit" data-testid="form-create-submit">
              {{ t('form.submit') }}
            </v-btn>
          </v-card-actions>
        </v-form>
      </v-card-text>
    </v-card>

    <v-alert
      v-if="storageUnavailable"
      type="warning"
      class="mb-4"
      data-testid="form-unavailable"
    >
      {{ t('storage.unavailable') }}
    </v-alert>

    <div v-else-if="store.loading" class="text-center py-8" data-testid="forms-loading">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <v-alert
      v-else-if="store.forms.length === 0"
      type="info"
      class="mb-4"
      data-testid="form-empty"
    >
      <div>{{ t('form.empty') }}</div>
      <v-btn
        variant="flat"
        color="primary"
        prepend-icon="mdi-plus"
        class="mt-3"
        data-testid="form-create-empty"
        @click="revealed = true"
      >
        {{ t('form.create') }}
      </v-btn>
    </v-alert>

    <v-card
      v-for="form in store.forms"
      v-else
      :key="form.id"
      class="rf-row"
      data-testid="form-row"
      :data-form-id="form.id"
    >
      <div class="rf-row__body">
        <div class="rf-row__main">
          <h3 class="text-subtitle-1 font-weight-medium" data-testid="form-title">
            {{ form.title }}
          </h3>

          <div class="rf-meta text-medium-emphasis mt-1">
            <span class="rf-meta__item" data-testid="form-field-count">
              <v-icon icon="mdi-form-select" size="16" />
              {{ t('form.fieldCount') }}: {{ form.fieldCount }}
            </span>
            <span class="rf-meta__item" data-testid="form-response-count">
              <v-icon icon="mdi-inbox-outline" size="16" />
              {{ t('form.responseCount') }}: {{ form.responseCount }}
            </span>
          </div>

          <span v-if="form.fieldCount === 0" class="rf-meta text-medium-emphasis mt-2" data-testid="form-no-fields">
            {{ t('form.noFields') }}
          </span>
          <a
            v-else
            class="rf-address mt-2"
            :href="share(form.formToken)"
            target="_blank"
            rel="noopener noreferrer"
            :aria-describedby="`newtab-${form.id}`"
            data-testid="form-link"
            >{{ share(form.formToken) }}</a
          >
          <span :id="`newtab-${form.id}`" class="d-sr-only">{{ t('share.newTab') }}</span>
        </div>

        <div class="rf-row__actions">
          <v-btn
            variant="tonal"
            prepend-icon="mdi-pencil-outline"
            data-testid="form-open"
            @click="router.push({ name: 'form-builder', params: { formId: form.id } })"
          >
            {{ t('form.open') }}
          </v-btn>
          <v-btn
            variant="text"
            color="error"
            prepend-icon="mdi-delete-outline"
            data-testid="form-delete"
            @click="deleting = form"
          >
            {{ t('form.delete') }}
          </v-btn>
        </div>
      </div>
    </v-card>

    <DeleteConfirm
      v-if="deleting"
      :title="deleting.title"
      :response-count="deleting.responseCount"
      :body="t('form.deleteConfirm', { title: deleting.title, count: deleting.responseCount })"
      testid="form-delete-confirm"
      @confirm="confirmDelete"
      @cancel="deleting = null"
    />
  </v-container>
</template>

<style scoped>
.rf-row + .rf-row { margin-block-start: var(--rf-stack); }

.rf-row__body {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  gap: 24px;
  padding: var(--rf-card-pad);
}

.rf-row__main { flex: 1 1 auto; min-width: 0; }

.rf-row__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  flex-shrink: 0;
}

@media (max-width: 600px) {
  .rf-row__body { flex-direction: column; gap: 16px; }
  .rf-row__actions { width: 100%; justify-content: space-between; }
}
</style>
