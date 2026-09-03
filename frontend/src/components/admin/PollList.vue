<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { usePollsStore } from '../../stores/polls'
import { useSessionStore } from '../../stores/session'
import PollForm from './PollForm.vue'

const { t } = useI18n()
const router = useRouter()
const polls = usePollsStore()
const session = useSessionStore()

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

function formatDeadline(value: string) {
  return new Date(value).toLocaleDateString('de-DE', { year: 'numeric', month: 'long', day: 'numeric' })
}
</script>

<template>
  <section class="poll-list">
    <header>
      <h1>{{ t('poll.listTitle') }}</h1>
      <button type="button" data-testid="sign-out" @click="signOut">{{ t('signIn.signOut') }}</button>
    </header>

    <PollForm />

    <p v-if="polls.polls.length === 0 && !polls.loading" data-testid="poll-list-empty">
      {{ t('poll.empty') }}
    </p>

    <ul v-else class="polls">
      <li v-for="poll in polls.polls" :key="poll.id" data-testid="poll-list-item" :data-poll-id="poll.id">
        <h3>{{ poll.title }}</h3>
        <p class="link" data-testid="poll-list-link">{{ linkFor(poll.participantToken) }}</p>
        <p class="meta">
          {{ poll.dayCount }} {{ t('poll.dayCount') }} · {{ poll.responseCount }} {{ t('poll.responseCount') }}
          · {{ t('poll.retention') }}: {{ formatDeadline(poll.retentionDeadline) }}
        </p>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.poll-list { font-family: system-ui, sans-serif; padding: 1rem; }
header { display: flex; justify-content: space-between; align-items: center; padding: 0 2rem; }
.polls { list-style: none; padding: 0 2rem; }
.polls li { border: 1px solid #cfd8dc; border-radius: 0.25rem; padding: 0.75rem; margin-bottom: 0.75rem; }
.link { word-break: break-all; font-family: ui-monospace, monospace; }
.meta { color: #546e7a; font-size: 0.9rem; }
button { padding: 0.5rem 0.75rem; font: inherit; cursor: pointer; }
</style>
