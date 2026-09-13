<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { usePollsStore } from '../../stores/polls'
import { useSessionStore } from '../../stores/session'
import PollForm from './PollForm.vue'
import ImportPanel from './ImportPanel.vue'
import DeleteConfirm from './DeleteConfirm.vue'
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
  <v-container max-width="1100" class="py-8">
    <div class="d-flex align-center justify-space-between mb-6">
      <h1 class="text-h4">{{ t('poll.listTitle') }}</h1>

      <!--
        The two ways a poll comes into being, as actions rather than as forms.

        Both used to stand permanently open above the list, which meant an operator scrolled past
        two forms to reach the polls they came for. The accepted cost is that creating a poll now
        takes one action where it took none - recorded in SC-005 rather than glossed over - and
        the empty state below cancels even that.
      -->
      <div class="d-flex ga-2">
        <v-btn
          variant="flat"
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
      </div>
    </div>

    <!--
      Unmounted when closed, not hidden. Leaving the entry in a hidden form would mean a poll
      half-written yesterday is one click from being created today (FR-014j).
    -->
    <!--
      Left open after a poll is created, deliberately. The form is where the new participant link
      appears, and that link is the reason the operator came - closing the form on success would
      hide the one thing they still need to copy.
    -->
    <PollForm v-if="revealed === 'create'" class="mb-6" />
    <!--
      Left open after a file is read, for the same reason the creation form is: the panel is where
      the summary and the *new* participant link appear, and 005 FR-011 makes that link the whole
      point - shared links for the original poll do not reach the imported one. Closing on success
      would hide the one thing the operator still has to copy.
    -->
    <ImportPanel v-if="revealed === 'import'" class="mb-6" @imported="polls.load()" />

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
      class="mb-4"
      data-testid="poll-list-item"
      :data-poll-id="poll.id"
    >
      <v-card-item>
        <v-card-title tag="h3">{{ poll.title }}</v-card-title>
        <v-card-subtitle>
          <span class="mr-3">
            <v-icon icon="mdi-calendar-range" size="small" class="mr-1" />
            {{ poll.dayCount }} {{ t('poll.dayCount') }}
          </span>
          <span class="mr-3">
            <v-icon icon="mdi-account-multiple-outline" size="small" class="mr-1" />
            {{ poll.responseCount }} {{ t('poll.responseCount') }}
          </span>
          <span>
            <v-icon icon="mdi-timer-sand" size="small" class="mr-1" />
            {{ t('poll.retention') }}: {{ d(new Date(poll.retentionDeadline), 'long') }}
          </span>
        </v-card-subtitle>
      </v-card-item>

      <v-card-text>
        <!--
          Same rule as everywhere else: what looks like a link is one (FR-015). The new-tab note
          is the link's description rather than part of it, so the link's text stays the bare
          address that people copy out of here (FR-016a, FR-017).
        -->
        <a
          class="link d-block"
          :href="linkFor(poll.participantToken)"
          target="_blank"
          rel="noopener noreferrer"
          :aria-describedby="`newtab-${poll.id}`"
          data-testid="poll-list-link"
          >{{ linkFor(poll.participantToken) }}</a
        >
        <span :id="`newtab-${poll.id}`" class="d-sr-only mb-3 d-block">
          {{ t('share.newTab') }}
        </span>
      </v-card-text>

      <v-card-actions>
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
        <v-spacer />
        <v-btn
          variant="text"
          color="error"
          prepend-icon="mdi-delete-outline"
          data-testid="delete-poll"
          @click="pendingDelete = { id: poll.id, title: poll.title, responseCount: poll.responseCount }"
        >
          {{ t('delete.poll') }}
        </v-btn>
      </v-card-actions>
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
.link { word-break: break-all; font-size: 0.85rem; color: rgb(var(--v-theme-secondary)); }
</style>
