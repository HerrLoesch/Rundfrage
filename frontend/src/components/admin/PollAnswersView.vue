<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import ResultGrid from '../poll/ResultGrid.vue'
import { useSessionStore } from '../../stores/session'
import {
  deleteResponse,
  fetchPollResults,
  type ApiProblem,
  type PollView,
} from '../../api/client'

const props = defineProps<{ pollId: string }>()

const { t } = useI18n()
const router = useRouter()
const session = useSessionStore()

const poll = ref<PollView | null>(null)
const loading = ref(true)
const problem = ref<ApiProblem | null>(null)

/**
 * Which page of answers is shown. Server-side paging at fifty is the design from 002 (research
 * R-7), so moving through the rows is a new request rather than a slice of something already
 * held here.
 */
const page = ref(1)

/**
 * The answers of one poll, at an address of their own (FR-014a).
 *
 * They used to unfold inside the poll card, held in that component's state, which made the
 * largest screen in the admin area the only one that could not be linked to or reloaded. Loading
 * from the address rather than from anything handed over is what makes a pasted link behave the
 * same as a clicked one.
 */
async function load() {
  loading.value = true
  problem.value = null

  try {
    poll.value = await fetchPollResults(props.pollId, page.value)
  } catch (failure) {
    const failed = failure as ApiProblem
    problem.value = failed

    if (failed.code === 'unauthorized') {
      // The server is the authority on the session, here as everywhere (FR-011).
      session.isSignedIn = false
      await router.push({ name: 'sign-in' })
      return
    }

    // A poll that is not there sends the operator to the list, which is where they can see what
    // is (FR-014g). An empty grid here would read as "nobody answered" - a claim about a poll
    // that does not exist.
    if (failed.code === 'not_found') {
      await router.push({ name: 'polls', query: { gone: '1' } })
    }
  } finally {
    loading.value = false
  }
}

onMounted(load)

async function changePage(next: number) {
  page.value = next
  await load()
}

async function removeResponse(responseId: string) {
  await deleteResponse(props.pollId, responseId)
  // Re-read rather than patch locally: the per-day totals move with it (004 FR-037b), and the
  // server is the only thing that knows the new numbers. Deleting the last answer therefore
  // leaves this page showing the grid's own empty state (FR-014f) without a special case.
  await load()
}
</script>

<template>
  <v-container class="rf-page rf-page--admin">
    <v-btn
      variant="text"
      prepend-icon="mdi-arrow-left"
      :to="{ name: 'polls' }"
      class="mb-4"
      data-testid="back-to-polls"
    >
      {{ t('poll.backToList') }}
    </v-btn>

    <v-alert
      v-if="problem && problem.code !== 'unauthorized' && problem.code !== 'not_found'"
      type="warning"
      data-testid="storage-unavailable"
    >
      {{ t('storage.unavailable') }}
    </v-alert>

    <div v-else-if="poll" data-testid="poll-answers">
      <h1 class="rf-title text-h4">{{ poll.title }}</h1>
      <p v-if="poll.message" class="text-body-1 mt-2 mb-4">{{ poll.message }}</p>

      <!--
        Export and delete-poll are deliberately not repeated here. They are one action away on the
        list, and duplicating them would be two places to keep in step for no gain (FR-014d).
      -->
      <ResultGrid
        :poll="poll"
        deletable
        class="mt-4"
        @delete-response="removeResponse"
        @change-page="changePage"
      />
    </div>

    <v-progress-linear v-else-if="loading" indeterminate data-testid="poll-answers-loading" />
  </v-container>
</template>
