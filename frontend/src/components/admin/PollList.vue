<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { usePollsStore } from '../../stores/polls'
import { useSessionStore } from '../../stores/session'
import PollForm from './PollForm.vue'
import DeleteConfirm from './DeleteConfirm.vue'
import ResultGrid from '../poll/ResultGrid.vue'
import { deletePoll, deleteResponse, fetchPollResults, type PollView } from '../../api/client'

const { t, d } = useI18n()
const router = useRouter()
const polls = usePollsStore()
const session = useSessionStore()

const openPollId = ref<string | null>(null)
const openResults = ref<PollView | null>(null)
const pendingDelete = ref<{ id: string; title: string; responseCount: number } | null>(null)

onMounted(async () => {
  await polls.load()

  // The server decides whether the session is valid; this view reacts to its answer. That is
  // what makes a reload work - the cookie is sent, the request succeeds, and nothing local
  // needed to remember anything.
  if (polls.problem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
  }
})

async function signOut() {
  await session.signOut()
  await router.push({ name: 'sign-in' })
}

function linkFor(token: string) {
  return `${window.location.origin}/u/${token}`
}

async function toggleResults(pollId: string) {
  if (openPollId.value === pollId) {
    openPollId.value = null
    openResults.value = null
    return
  }

  openPollId.value = pollId
  openResults.value = await fetchPollResults(pollId)
}

async function removeResponse(responseId: string) {
  if (!openPollId.value) return
  await deleteResponse(openPollId.value, responseId)
  // Reload rather than patch locally: the per-day totals move with it (FR-037b), and the
  // server is the only thing that knows the new numbers.
  openResults.value = await fetchPollResults(openPollId.value)
  await polls.load()
}

async function confirmDelete() {
  if (!pendingDelete.value) return
  await deletePoll(pendingDelete.value.id)
  pendingDelete.value = null
  openPollId.value = null
  openResults.value = null
  await polls.load()
}
</script>

<template>
  <v-container max-width="1100" class="py-8">
    <div class="d-flex align-center justify-space-between mb-6">
      <h1 class="text-h4">{{ t('poll.listTitle') }}</h1>
      <v-btn
        variant="outlined"
        prepend-icon="mdi-logout"
        data-testid="sign-out"
        @click="signOut"
      >
        {{ t('signIn.signOut') }}
      </v-btn>
    </div>

    <PollForm />

    <v-alert
      v-if="polls.polls.length === 0 && !polls.loading"
      type="info"
      data-testid="poll-list-empty"
    >
      {{ t('poll.empty') }}
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
        <code class="link d-block mb-3" data-testid="poll-list-link">
          {{ linkFor(poll.participantToken) }}
        </code>

        <ResultGrid
          v-if="openPollId === poll.id && openResults"
          :poll="openResults"
          deletable
          class="mt-4"
          @delete-response="removeResponse"
        />
      </v-card-text>

      <v-card-actions>
        <v-btn
          variant="tonal"
          prepend-icon="mdi-table-eye"
          data-testid="show-results"
          @click="toggleResults(poll.id)"
        >
          {{ t('results.title') }}
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
