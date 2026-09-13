<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'
import { useWishListsStore, filledPercent, isComplete } from '../../stores/wishLists'
import { useSessionStore } from '../../stores/session'
import WishListForm from './WishListForm.vue'
import PageHeader from '../layout/PageHeader.vue'
import type { WishListSummary } from '../../api/client'

const { t } = useI18n()
const route = useRoute()
const router = useRouter()
const store = useWishListsStore()
const session = useSessionStore()

const revealed = ref(false)
const deleting = ref<WishListSummary | null>(null)

/**
 * The server is the authority on the session, so an unauthorised read sends the operator to the
 * sign-in form rather than a guard deciding it from client state (007 FR-011).
 */
onMounted(async () => {
  await store.load()

  if (store.loadProblem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
  }
})

/**
 * "Nothing is stored" and "this cannot be read right now" show no lists and mean opposite things
 * (FR-046). Derived rather than stored, so the two can never disagree.
 */
const storageUnavailable = computed(
  () => store.loadProblem !== null && store.loadProblem.code !== 'unauthorized',
)

/**
 * Set by the detail page when the list at its address does not exist. Said here rather than
 * there, because this is the page that can show what *does* exist - the same arrangement the poll
 * area uses (007 FR-014g).
 */
const listIsGone = computed(() => route.query.gone === '1')

/**
 * The absolute address, as `PollList.vue` builds it for a poll and for the same reason: this is
 * the text the operator selects and pastes into a chat, and a bare `/w/...` path is useless
 * anywhere outside the browser that rendered it (FR-017).
 */
function share(listToken: string): string {
  return `${window.location.origin}/w/${listToken}`
}

/**
 * Revealed on demand and unmounted when closed: a list half-written yesterday must not be one
 * click from being created today (FR-042).
 */
function reveal() {
  revealed.value = !revealed.value
}

async function confirmDelete() {
  const list = deleting.value
  if (!list) return
  await store.remove(list.id)
  deleting.value = null
}
</script>

<template>
  <v-container class="rf-page rf-page--admin">
    <PageHeader :title="t('wish.listTitle')">
      <template #actions>
        <v-btn
          color="primary"
          prepend-icon="mdi-plus"
          data-testid="wish-create-toggle"
          @click="reveal"
        >
          {{ revealed ? t('wish.createHide') : t('wish.create') }}
        </v-btn>
      </template>
    </PageHeader>

    <!-- Unmounted when closed, not hidden. Leaving the entry in a hidden form would mean a wish
         list half-written yesterday is one click from being created today (FR-042). -->
    <WishListForm v-if="revealed" class="rf-section-gap" />

    <v-alert
      v-if="listIsGone"
      type="info"
      class="mb-4"
      data-testid="wish-list-gone"
    >
      {{ t('wish.gone') }}
    </v-alert>

    <!-- "Nothing is stored" and "this cannot be read" are different things and must look it
         (FR-046). Neither is ever shown as a zero. -->
    <v-alert
      v-if="storageUnavailable"
      type="warning"
      class="mb-4"
      data-testid="wish-lists-unavailable"
    >
      {{ t('storage.unavailable') }}
    </v-alert>

    <div v-else-if="store.loading" class="text-center py-8" data-testid="wish-lists-loading">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <!-- The empty state offers creating directly (FR-042a). With nothing stored there is nothing for
         the action above to be preferable to, so making the operator find it first would be a step
         that buys nothing. -->
    <v-alert
      v-else-if="store.lists.length === 0"
      type="info"
      class="mb-4"
      data-testid="wish-lists-empty"
    >
      <div>{{ t('wish.empty') }}</div>
      <v-btn
        variant="flat"
        color="primary"
        prepend-icon="mdi-plus"
        class="mt-3"
        data-testid="wish-create-empty"
        @click="revealed = true"
      >
        {{ t('wish.create') }}
      </v-btn>
    </v-alert>

    <v-card
      v-for="list in store.lists"
      v-else
      :key="list.id"
      class="rf-row"
      data-testid="wish-list-row"
      :data-wish-list-id="list.id"
    >
      <!--
        One padded block, not three.

        The row was a v-card-item over a v-card-text over a v-card-actions - three zones, each
        with its own padding, and the actions pushed to opposite edges by a spacer. On a 1100px
        page that put "öffnen" and "löschen" seventy centimetres apart on screen with nothing
        between them, which is what made the list read as scattered rather than dense.
      -->
      <div class="rf-row__body">
        <div class="rf-row__main">
          <div class="rf-row__title">
            <h3 class="text-subtitle-1 font-weight-medium">{{ list.title }}</h3>

            <!-- Closed in words, beside the title, never by colour alone (FR-028b, FR-057). -->
            <v-chip
              v-if="list.closed"
              :data-testid="`wish-closed-${list.id}`"
            >
              {{ t('wish.closed') }}
            </v-chip>
            <!-- Said in words, not left to 100 % (FR-045b). -->
            <v-chip
              v-if="isComplete(list.entryCount, list.placeCount)"
              color="success"
              variant="tonal"
              :data-testid="`wish-complete-${list.id}`"
            >
              {{ t('wish.complete') }}
            </v-chip>
          </div>

          <div class="rf-meta text-medium-emphasis mt-1">
            <span class="rf-meta__item">
              <v-icon icon="mdi-calendar" size="16" />
              {{ list.targetDate }}
            </span>
            <span class="rf-meta__item" :data-testid="`wish-entries-${list.id}`">
              <v-icon icon="mdi-account-multiple-outline" size="16" />
              {{ t('wish.filled', {
                filled: list.entryCount,
                places: list.placeCount,
                percent: filledPercent(list.entryCount, list.placeCount),
              }) }}
            </span>
            <!-- The other reading of the same question, labelled separately so neither can be
                 mistaken for the other (FR-045a). -->
            <span class="rf-meta__item" :data-testid="`wish-untaken-${list.id}`">
              <v-icon icon="mdi-tray-full" size="16" />
              {{ t('wish.untaken', { count: list.untakenItemCount }) }}
            </span>
          </div>

          <!-- Same rule as everywhere else: what looks like a link is one (FR-015). The new-tab
               note is the link's description rather than part of it, so the link's text stays the
               bare address that people copy out of here (FR-016a, FR-017).

               A bare anchor rather than ShareLink.vue, which is the pattern PollList.vue already
               sets for a row: ShareLink announces one newly minted link, and two hundred of those
               banners stacked down a list is not that component's job. -->
          <a
            class="rf-address mt-2"
            :href="share(list.listToken)"
            target="_blank"
            rel="noopener noreferrer"
            :aria-describedby="`newtab-${list.id}`"
            :data-testid="`wish-share-${list.id}`"
            >{{ share(list.listToken) }}</a
          >
          <span :id="`newtab-${list.id}`" class="d-sr-only">{{ t('share.newTab') }}</span>
        </div>

        <!-- Kept together at the end of the row, so the eye finds the controls in one place
             instead of tracking across the full width to each of them in turn. -->
        <div class="rf-row__actions">
          <v-btn
            variant="tonal"
            prepend-icon="mdi-table-eye"
            :data-testid="`wish-open-${list.id}`"
            @click="router.push({ name: 'wish-list', params: { wishListId: list.id } })"
          >
            {{ t('wish.detailTitle') }}
          </v-btn>
          <v-btn
            variant="text"
            color="error"
            prepend-icon="mdi-delete-outline"
            :data-testid="`wish-delete-${list.id}`"
            @click="deleting = list"
          >
            {{ t('wish.deleteList') }}
          </v-btn>
        </div>
      </div>
    </v-card>

    <!--
      Its own dialog rather than DeleteConfirm, for one word: that component states the count in
      "Antworten", and what dies with a wish list is Zusagen. FR-038 asks the confirmation to say
      what will be destroyed, and saying it with the wrong noun is not saying it.
    -->
    <v-dialog :model-value="deleting !== null" max-width="520" persistent
              @update:model-value="deleting = null">
      <v-card v-if="deleting" data-testid="wish-delete-confirm" role="alertdialog">
        <v-card-item>
          <template #prepend>
            <v-icon icon="mdi-alert-circle-outline" color="error" size="large" />
          </template>
          <v-card-title tag="h3">{{ t('delete.confirmTitle') }}</v-card-title>
        </v-card-item>

        <v-card-text data-testid="wish-delete-confirm-body">
          {{ t('wish.deleteListConfirm', { title: deleting.title, count: deleting.entryCount }) }}
        </v-card-text>

        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" data-testid="wish-delete-cancel" @click="deleting = null">
            {{ t('delete.cancel') }}
          </v-btn>
          <v-btn color="error" data-testid="wish-delete-confirmed" @click="confirmDelete">
            {{ t('delete.confirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<style scoped>
/* The list itself: one gap between rows, spent from the shared scale. */
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

/* Below the breakpoint the actions go under the content rather than squeezing it (FR-058). */
@media (max-width: 600px) {
  .rf-row__body { flex-direction: column; gap: 16px; }
  .rf-row__actions { width: 100%; justify-content: space-between; }
}
</style>
