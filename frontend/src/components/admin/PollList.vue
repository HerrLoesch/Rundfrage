<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { usePollsStore } from '../../stores/polls'
import { useSessionStore } from '../../stores/session'
import PollForm from './PollForm.vue'
import ImportPanel from './ImportPanel.vue'
import DeleteConfirm from './DeleteConfirm.vue'
import PageHeader from '../layout/PageHeader.vue'
import { deletePoll, exportUrl } from '../../api/client'

const { t, d } = useI18n()
const route = useRoute()
const router = useRouter()

/**
 * Set by the answers page when the poll at its address does not exist (FR-014g). Saying so here
 * rather than there is deliberate: this is the page that can show what *does* exist.
 */
const pollIsGone = computed(() => route.query.gone === '1')
const polls = usePollsStore()
const session = useSessionStore()

/**
 * FR-024a. An empty list and an unreachable store both show no polls and mean opposite things:
 * "you have not created any yet" against "your data cannot be read right now". Deriving this
 * rather than storing it means the two can never disagree.
 */
const storageUnavailable = computed(
  () => polls.loadProblem !== null && polls.loadProblem.code !== 'unauthorized',
)

/**
 * Which form is revealed, if either (007 FR-014h, FR-014i).
 *
 * One value rather than two booleans, because at most one may be open and two booleans can
 * represent the state where both are - which the interface would then have to prevent by
 * remembering to. Closing is `null`, and that also discards what was entered, because the forms
 * are unmounted rather than hidden (FR-014j).
 */
const revealed = ref<'create' | 'import' | null>(null)

function reveal(which: 'create' | 'import') {
  revealed.value = revealed.value === which ? null : which
}

const pendingDelete = ref<{ id: string; title: string; responseCount: number } | null>(null)

onMounted(async () => {
  await polls.load()

  // The server decides whether the session is valid; this view reacts to its answer. That is
  // what makes a reload work - the cookie is sent, the request succeeds, and nothing local
  // needed to remember anything (FR-011).
  if (polls.loadProblem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
  }
})

function linkFor(token: string) {
  return `${window.location.origin}/u/${token}`
}

async function confirmDelete() {
  if (!pendingDelete.value) return
  await deletePoll(pendingDelete.value.id)
  pendingDelete.value = null
  await polls.load()
}
</script>

<template>
  <v-container class="rf-page rf-page--admin">
    <PageHeader :title="t('poll.listTitle')">

      <!--
        The two ways a poll comes into being, as actions rather than as forms.

        Both used to stand permanently open above the list, which meant an operator scrolled past
        two forms to reach the polls they came for. The accepted cost is that creating a poll now
        takes one action where it took none - recorded in SC-005 rather than glossed over - and
        the empty state below cancels even that.
      -->
      <template #actions>
        <v-btn
          color="primary"
          prepend-icon="mdi-plus"
          data-testid="poll-create-toggle"
          @click="reveal('create')"
        >
          {{ revealed === 'create' ? t('poll.createHide') : t('poll.create') }}
        </v-btn>
        <v-btn
          variant="outlined"
          prepend-icon="mdi-file-upload-outline"
          data-testid="poll-import-toggle"
          @click="reveal('import')"
        >
          {{ revealed === 'import' ? t('poll.importHide') : t('poll.importShow') }}
        </v-btn>
      </template>
    </PageHeader>

    <!--
      Unmounted when closed, not hidden. Leaving the entry in a hidden form would mean a poll
      half-written yesterday is one click from being created today (FR-014j).
    -->
    <!--
      Left open after a poll is created, deliberately. The form is where the new participant link
      appears, and that link is the reason the operator came - closing the form on success would
      hide the one thing they still need to copy.
    -->
    <PollForm v-if="revealed === 'create'" class="rf-section-gap" />
    <!--
      Left open after a file is read, for the same reason the creation form is: the panel is where
      the summary and the *new* participant link appear, and 005 FR-011 makes that link the whole
      point - shared links for the original poll do not reach the imported one. Closing on success
      would hide the one thing the operator still has to copy.
    -->
    <ImportPanel v-if="revealed === 'import'" class="rf-section-gap" @imported="polls.load()" />

    <v-alert
      v-if="pollIsGone"
      type="info"
      class="mb-4"
      data-testid="poll-gone"
    >
      {{ t('poll.gone') }}
    </v-alert>

    <v-alert
      v-if="storageUnavailable"
      type="warning"
      data-testid="storage-unavailable"
    >
      {{ t('storage.unavailable') }}
    </v-alert>

    <!--
      The empty state offers creating directly (FR-014l). With nothing stored there is nothing for
      the action above to be preferable to, so making the operator find it first would be a step
      that buys nothing.
    -->
    <v-alert
      v-else-if="polls.polls.length === 0 && !polls.loading"
      type="info"
      data-testid="poll-list-empty"
    >
      <div>{{ t('poll.empty') }}</div>
      <v-btn
        variant="flat"
        color="primary"
        prepend-icon="mdi-plus"
        class="mt-3"
        data-testid="poll-create-empty"
        @click="reveal('create')"
      >
        {{ t('poll.create') }}
      </v-btn>
    </v-alert>

    <v-card
      v-for="poll in polls.polls"
      :key="poll.id"
      class="rf-row"
      data-testid="poll-list-item"
      :data-poll-id="poll.id"
    >
      <!-- One padded block with the controls grouped at its end, the same arrangement the
           wish-list rows use. Three stacked card zones with a spacer between the actions put
           "Antworten" and "löschen" at opposite edges of an 1100px page. -->
      <div class="rf-row__body">
        <div class="rf-row__main">
          <h3 class="text-subtitle-1 font-weight-medium">{{ poll.title }}</h3>

          <div class="rf-meta text-medium-emphasis mt-1">
            <span class="rf-meta__item">
              <v-icon icon="mdi-calendar-range" size="16" />
              {{ poll.dayCount }} {{ t('poll.dayCount') }}
            </span>
            <span class="rf-meta__item">
              <v-icon icon="mdi-account-multiple-outline" size="16" />
              {{ poll.responseCount }} {{ t('poll.responseCount') }}
            </span>
            <span class="rf-meta__item">
              <v-icon icon="mdi-timer-sand" size="16" />
              {{ t('poll.retention') }}: {{ d(new Date(poll.retentionDeadline), 'long') }}
            </span>
            <!--
              Who owns it (009 FR-039). Null means the operator's own, and is rendered as a word
              rather than as a blank cell - a blank cell reads as missing data rather than as
              "mine".
            -->
            <span class="rf-meta__item" data-testid="poll-owner">
              <v-icon icon="mdi-account-key-outline" size="16" />
              {{ t('creator.ownerLabel') }}: {{ poll.creatorName ?? t('creator.ownerSelf') }}
            </span>
          </div>

          <!--
            Same rule as everywhere else: what looks like a link is one (FR-015). The new-tab note
            is the link's description rather than part of it, so the link's text stays the bare
            address that people copy out of here (FR-016a, FR-017).
          -->
          <a
            class="rf-address mt-2"
            :href="linkFor(poll.participantToken)"
            target="_blank"
            rel="noopener noreferrer"
            :aria-describedby="`newtab-${poll.id}`"
            data-testid="poll-list-link"
            >{{ linkFor(poll.participantToken) }}</a
          >
          <span :id="`newtab-${poll.id}`" class="d-sr-only">{{ t('share.newTab') }}</span>
        </div>

        <div class="rf-row__actions">
          <!--
            A link now, not an expander. The answers were the largest thing on this page and the
            only one without an address of its own - so they could not be linked to, bookmarked or
            reloaded, which every other area can (007 FR-014a, SC-006).
          -->
          <v-btn
            variant="tonal"
            prepend-icon="mdi-table-eye"
            :to="{ name: 'poll-answers', params: { pollId: poll.id } }"
            data-testid="show-results"
          >
            {{ t('results.title') }}
          </v-btn>
          <!--
            Export and delete stay here rather than moving to the answers page. Moving them would
            have added a step to two tasks that have none today (FR-014d, SC-005).
          -->
          <v-btn
            variant="tonal"
            prepend-icon="mdi-code-json"
            :href="exportUrl(poll.id)"
            data-testid="export-poll"
          >
            {{ t('export.poll') }}
          </v-btn>
          <v-btn
            variant="text"
            color="error"
            prepend-icon="mdi-delete-outline"
            data-testid="delete-poll"
            @click="pendingDelete = { id: poll.id, title: poll.title, responseCount: poll.responseCount }"
          >
            {{ t('delete.poll') }}
          </v-btn>
        </div>
      </div>
    </v-card>

    <DeleteConfirm
      v-if="pendingDelete"
      :title="pendingDelete.title"
      :response-count="pendingDelete.responseCount"
      @confirm="confirmDelete"
      @cancel="pendingDelete = null"
    />
  </v-container>
</template>

<style scoped>
/* Identical to the wish-list rows, because they are the same kind of thing (007 FR-002). */
.rf-row + .rf-row { margin-block-start: var(--rf-stack); }

.rf-row__body {
  display: flex;
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
