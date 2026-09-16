<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useCreatorsStore } from '../../stores/creators'
import { useSessionStore } from '../../stores/session'
import DeleteConfirm from './DeleteConfirm.vue'
import PageHeader from '../layout/PageHeader.vue'
import type { CreatorSummary } from '../../api/client'

const { t } = useI18n()
const router = useRouter()
const store = useCreatorsStore()
const session = useSessionStore()

const revealed = ref(false)
const draftName = ref('')
const renaming = ref<CreatorSummary | null>(null)
const renameName = ref('')

/**
 * Three separate confirmation targets, deliberately not one with a mode.
 *
 * FR-020b requires that revoking and deleting cannot read as variants of one another, and a single
 * `pending` object with a `kind` field is exactly the shape that invites a template to render them
 * with one block and one label. Three refs cost nothing and make the distinction structural.
 */
const revoking = ref<CreatorSummary | null>(null)
const reissuing = ref<CreatorSummary | null>(null)
const deleting = ref<CreatorSummary | null>(null)

onMounted(async () => {
  await store.load()

  // The server is the authority on the session (007 FR-011).
  if (store.loadProblem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
  }
})

/** "Nothing is stored" and "this cannot be read" mean opposite things (FR-046). */
const storageUnavailable = computed(
  () => store.loadProblem !== null && store.loadProblem.code !== 'unauthorized',
)

/**
 * The absolute address, as the poll and wish-list areas build theirs: this is the text the
 * operator selects and sends in a message, and a bare `/e/...` path is useless outside the browser
 * that rendered it.
 */
function share(linkToken: string): string {
  return `${window.location.origin}/e/${linkToken}`
}

function reveal() {
  revealed.value = !revealed.value
  if (!revealed.value) draftName.value = ''
}

async function create() {
  const created = await store.create(draftName.value)
  if (created) {
    draftName.value = ''
    revealed.value = false
  }
}

function startRename(creator: CreatorSummary) {
  renaming.value = creator
  renameName.value = creator.name
}

async function confirmRename() {
  const creator = renaming.value
  if (!creator) return
  if (await store.rename(creator.id, renameName.value)) renaming.value = null
}

async function confirmRevoke() {
  const creator = revoking.value
  if (!creator) return
  await store.revoke(creator.id)
  revoking.value = null
}

async function confirmReissue() {
  const creator = reissuing.value
  if (!creator) return
  await store.reissue(creator.id)
  reissuing.value = null
}

async function confirmDelete() {
  const creator = deleting.value
  if (!creator) return
  await store.remove(creator.id)
  deleting.value = null
}

/** Translated here so the message carries this Ersteller's own two counts (FR-019, FR-020a). */
function revokeBody(creator: CreatorSummary): string {
  return t('creator.revokeBody', {
    name: creator.name,
    pollCount: creator.pollCount,
    wishListCount: creator.wishListCount,
  })
}

function deleteBody(creator: CreatorSummary): string {
  return t('creator.deleteBody', {
    name: creator.name,
    pollCount: creator.pollCount,
    wishListCount: creator.wishListCount,
  })
}

const problemText = computed(() => {
  const problem = store.problem
  if (!problem) return null
  return t(`error.${problem.code}`, {
    limit: problem.limit ?? 0,
    detail: problem.detail ?? '',
  })
})
</script>

<template>
  <v-container class="rf-page rf-page--admin">
    <PageHeader :title="t('creator.listTitle')" :subtitle="t('creator.intro')">
      <template #actions>
        <v-btn
          color="primary"
          prepend-icon="mdi-plus"
          data-testid="creator-create-open"
          @click="reveal"
        >
          {{ t('creator.create') }}
        </v-btn>
      </template>
    </PageHeader>

    <!-- Revealed on demand and unmounted when closed, so a name half-typed yesterday is not one
         click from creating an Ersteller today (FR-045). -->
    <v-card v-if="revealed" class="rf-section-gap mb-4" data-testid="creator-form">
      <v-card-text>
        <v-text-field
          v-model="draftName"
          :label="t('creator.name')"
          :hint="t('creator.nameHint')"
          persistent-hint
          maxlength="100"
          data-testid="creator-name-input"
          @keyup.enter="create"
        />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="reveal">{{ t('creator.cancel') }}</v-btn>
        <v-btn color="primary" data-testid="creator-create-submit" @click="create">
          {{ t('creator.create') }}
        </v-btn>
      </v-card-actions>
    </v-card>

    <!-- The cap's refusal lands here, and says what actually frees a place (FR-009a). -->
    <v-alert
      v-if="problemText"
      type="error"
      class="mb-4"
      data-testid="creator-limit-refusal"
    >
      {{ problemText }}
    </v-alert>

    <v-alert
      v-if="storageUnavailable"
      type="warning"
      class="mb-4"
      data-testid="creator-unavailable"
    >
      {{ t('creator.unavailable') }}
    </v-alert>

    <div v-else-if="store.loading" class="text-center py-8" data-testid="creator-loading">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <!-- FR-046: "none exist" says so in words and offers creating directly. Never a zero. -->
    <v-alert
      v-else-if="store.creators.length === 0"
      type="info"
      class="mb-4"
      data-testid="creator-empty"
    >
      <div>{{ t('creator.empty') }}</div>
      <v-btn
        variant="flat"
        color="primary"
        prepend-icon="mdi-plus"
        class="mt-3"
        data-testid="creator-create-empty"
        @click="revealed = true"
      >
        {{ t('creator.emptyAction') }}
      </v-btn>
    </v-alert>

    <div v-else data-testid="creator-list">
      <v-card
        v-for="creator in store.creators"
        :key="creator.id"
        class="rf-row"
        data-testid="creator-row"
        :data-creator-id="creator.id"
      >
        <div class="rf-row__body">
          <div class="rf-row__main">
            <div class="rf-row__title">
              <h3 class="text-subtitle-1 font-weight-medium" data-testid="creator-name">
                {{ creator.name }}
              </h3>

              <!-- In words, never by colour alone (FR-057). A revoked Ersteller is listed like any
                   other with its counts, because the operator's next decision - new link, or
                   delete - needs exactly those figures (FR-044a). -->
              <v-chip v-if="!creator.hasLink" data-testid="creator-no-link">
                {{ t('creator.noLink') }}
              </v-chip>
            </div>

            <div class="rf-meta text-medium-emphasis mt-1">
              <span class="rf-meta__item" data-testid="creator-created">
                <v-icon icon="mdi-calendar" size="16" />
                {{ new Date(creator.createdAt).toLocaleDateString('de-DE') }}
              </span>
              <span class="rf-meta__item" data-testid="creator-poll-count">
                <v-icon icon="mdi-calendar-multiselect" size="16" />
                {{ t('creator.pollCount') }}: {{ creator.pollCount }}
              </span>
              <span class="rf-meta__item" data-testid="creator-wish-list-count">
                <v-icon icon="mdi-gift-outline" size="16" />
                {{ t('creator.wishListCount') }}: {{ creator.wishListCount }}
              </span>
            </div>

            <!-- Shown only while there is one. The operator re-copies the link from here rather
                 than keeping their own note of it (FR-044). Same rule as everywhere else: what
                 looks like a link is one (FR-015), rendered with the shared rf-address style. -->
            <template v-if="creator.hasLink && creator.linkToken">
              <a
                class="rf-address mt-2"
                :href="share(creator.linkToken)"
                target="_blank"
                rel="noopener noreferrer"
                :aria-describedby="`newtab-${creator.id}`"
                data-testid="creator-link"
                >{{ share(creator.linkToken) }}</a
              >
              <span :id="`newtab-${creator.id}`" class="d-sr-only">{{ t('share.newTab') }}</span>
            </template>
          </div>

          <!--
            Revoking and deleting are separate, separately labelled actions, and they are NOT
            adjacent (FR-020b). Rename and the link actions sit between them, and only the deletion
            carries destructive weight.
          -->
          <div class="rf-row__actions">
            <v-btn
              variant="text"
              size="small"
              data-testid="creator-rename"
              @click="startRename(creator)"
            >
              {{ t('creator.rename') }}
            </v-btn>
            <v-btn
              variant="text"
              size="small"
              data-testid="creator-reissue"
              @click="reissuing = creator"
            >
              {{ t('creator.reissue') }}
            </v-btn>
            <v-btn
              v-if="creator.hasLink"
              variant="text"
              size="small"
              data-testid="creator-revoke"
              @click="revoking = creator"
            >
              {{ t('creator.revoke') }}
            </v-btn>
            <v-btn
              variant="text"
              size="small"
              color="error"
              data-testid="creator-delete"
              @click="deleting = creator"
            >
              {{ t('creator.delete') }}
            </v-btn>
          </div>
        </div>
      </v-card>
    </div>

    <v-dialog :model-value="renaming !== null" max-width="480" @update:model-value="renaming = null">
      <v-card data-testid="creator-rename-dialog">
        <v-card-title tag="h3">{{ t('creator.renameTitle') }}</v-card-title>
        <v-card-text>
          <v-text-field
            v-model="renameName"
            :label="t('creator.name')"
            maxlength="100"
            data-testid="creator-rename-input"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="renaming = null">{{ t('creator.cancel') }}</v-btn>
          <v-btn color="primary" data-testid="creator-rename-submit" @click="confirmRename">
            {{ t('creator.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!--
      Three confirmations, and the only one drawn as a destruction is the one that destroys.

      Revoking states that NOTHING it owns will be removed; reissuing states that the old link
      stops working and the content survives; deleting names both counts and says that an export
      must be taken first if anything is to be kept (FR-019, FR-020a, FR-020b, FR-020c).
    -->
    <DeleteConfirm
      v-if="revoking"
      :title="revoking.name"
      :heading="t('creator.revokeTitle')"
      :body="revokeBody(revoking)"
      :confirm-label="t('creator.revokeConfirm')"
      testid="creator-revoke-confirm"
      @confirm="confirmRevoke"
      @cancel="revoking = null"
    />

    <DeleteConfirm
      v-if="reissuing"
      :title="reissuing.name"
      :heading="t('creator.reissueTitle')"
      :body="t('creator.reissueBody', { name: reissuing.name })"
      :confirm-label="t('creator.reissueConfirm')"
      testid="creator-reissue-confirm"
      @confirm="confirmReissue"
      @cancel="reissuing = null"
    />

    <DeleteConfirm
      v-if="deleting"
      :title="deleting.name"
      :heading="t('creator.deleteTitle')"
      :body="deleteBody(deleting)"
      :note="t('creator.deleteKeepHint')"
      :confirm-label="t('creator.deleteConfirm')"
      testid="creator-delete-confirm"
      @confirm="confirmDelete"
      @cancel="deleting = null"
    />
  </v-container>
</template>

<style scoped>
/* Identical to the poll and wish-list rows, because they are the same kind of thing (007 FR-002). */
.rf-row + .rf-row { margin-block-start: var(--rf-stack); }

.rf-row__body {
  display: flex;
  align-items: flex-start;
  gap: 24px;
  padding: var(--rf-card-pad);
}

.rf-row__main { flex: 1 1 auto; min-width: 0; }

.rf-row__title {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

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
