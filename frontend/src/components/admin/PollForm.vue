<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { usePollsStore } from '../../stores/polls'
import { useProblemText } from '../../composables/useProblemText'

const { t } = useI18n()
const polls = usePollsStore()
const problemText = useProblemText()

const title = ref('')
const message = ref('')
const dayInput = ref('')
const days = ref<string[]>([])

function addDay() {
  const day = dayInput.value
  if (!day) return
  // FR-012 and FR-013 made visible before anything is sent: stored once, shown chronologically.
  if (!days.value.includes(day)) {
    days.value = [...days.value, day].sort()
  }
  dayInput.value = ''
}

function removeDay(day: string) {
  days.value = days.value.filter((d) => d !== day)
}

const errorText = computed(() => problemText(polls.problem))

const shareLink = computed(() =>
  polls.created ? `${window.location.origin}/u/${polls.created.participantToken}` : '',
)

const retentionText = computed(() =>
  polls.created
    ? new Date(polls.created.retentionDeadline).toLocaleDateString('de-DE', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      })
    : '',
)

async function submit() {
  await polls.create(title.value, message.value || null, days.value)
}
</script>

<template>
  <form class="poll-form" data-testid="poll-form" @submit.prevent="submit">
    <h2>{{ t('poll.createTitle') }}</h2>

    <label for="poll-title">{{ t('poll.title') }}</label>
    <input id="poll-title" v-model="title" data-testid="poll-title" type="text" />

    <label for="poll-message">{{ t('poll.message') }}</label>
    <textarea id="poll-message" v-model="message" data-testid="poll-message" rows="3"></textarea>

    <fieldset>
      <legend>{{ t('poll.days') }}</legend>
      <div class="day-add">
        <input v-model="dayInput" data-testid="poll-day-input" type="date" :aria-label="t('poll.addDay')" />
        <button type="button" data-testid="poll-add-day" @click="addDay">{{ t('poll.addDay') }}</button>
      </div>

      <ul class="days">
        <li v-for="day in days" :key="day" data-testid="poll-day" :data-date="day">
          <span>{{ day }}</span>
          <button type="button" :aria-label="t('poll.removeDay')" @click="removeDay(day)">×</button>
        </li>
      </ul>
    </fieldset>

    <button type="submit" data-testid="poll-submit">{{ t('poll.submit') }}</button>

    <p v-if="errorText" class="error" role="alert" data-testid="poll-error">{{ errorText }}</p>

    <div v-if="polls.created" class="created" data-testid="poll-created">
      <p class="label">{{ t('poll.shareLink') }}</p>
      <p data-testid="poll-share-link">{{ shareLink }}</p>
      <p data-testid="poll-retention">{{ t('poll.retention') }}: {{ retentionText }}</p>
    </div>
  </form>
</template>

<style scoped>
.poll-form { display: flex; flex-direction: column; gap: 0.5rem; max-width: 32rem; padding: 2rem; font-family: system-ui, sans-serif; }
label, legend { font-weight: 600; }
input, textarea { padding: 0.5rem; font: inherit; }
.day-add { display: flex; gap: 0.5rem; }
.days { list-style: none; padding: 0; display: flex; flex-wrap: wrap; gap: 0.5rem; }
.days li { display: flex; gap: 0.35rem; align-items: center; background: #eceff1; padding: 0.25rem 0.5rem; border-radius: 0.25rem; }
button { padding: 0.5rem 0.75rem; font: inherit; cursor: pointer; }
.error { background: #fdecea; color: #b71c1c; border-left: 4px solid #c62828; padding: 0.5rem 0.75rem; }
.created { background: #e6f4ea; border-left: 4px solid #2e7d32; padding: 0.75rem; word-break: break-all; }
.label { font-weight: 600; margin: 0 0 0.25rem; }
</style>
