<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { usePollsStore } from '../../stores/polls'
import { useProblemText } from '../../composables/useProblemText'
import ShareLink from '../poll/ShareLink.vue'

const { t, d } = useI18n()
const polls = usePollsStore()
const problemText = useProblemText()

const title = ref('')
const message = ref('')
const dayInput = ref('')
const days = ref<string[]>([])
const busy = ref(false)

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
  days.value = days.value.filter((entry) => entry !== day)
}

const errorText = computed(() => problemText(polls.problem))

const retentionText = computed(() =>
  polls.created ? d(new Date(polls.created.retentionDeadline), 'long') : '',
)

function formatDay(day: string): string {
  return d(new Date(`${day}T12:00:00`), 'long')
}

async function submit() {
  busy.value = true
  try {
    if (await polls.create(title.value, message.value || null, days.value)) {
      title.value = ''
      message.value = ''
      days.value = []
    }
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <v-card class="mb-6">
    <v-card-item>
      <v-card-title tag="h2">{{ t('poll.createTitle') }}</v-card-title>
    </v-card-item>

    <v-card-text>
      <v-form data-testid="poll-form" @submit.prevent="submit">
        <v-text-field
          v-model="title"
          :label="t('poll.title')"
          data-testid="poll-title"
          maxlength="300"
          counter
          class="mb-4"
        />

        <v-textarea
          v-model="message"
          :label="t('poll.message')"
          data-testid="poll-message"
          maxlength="2000"
          rows="3"
          counter
          class="mb-4"
        />

        <v-label class="mb-2 font-weight-medium">{{ t('poll.days') }}</v-label>
        <div class="d-flex ga-2 align-start mb-3">
          <v-text-field
            v-model="dayInput"
            type="date"
            :aria-label="t('poll.addDay')"
            data-testid="poll-day-input"
            density="comfortable"
          />
          <v-btn
            color="secondary"
            size="large"
            data-testid="poll-add-day"
            prepend-icon="mdi-plus"
            @click="addDay"
          >
            {{ t('poll.addDay') }}
          </v-btn>
        </div>

        <div class="d-flex flex-wrap ga-2 mb-4">
          <v-chip
            v-for="day in days"
            :key="day"
            closable
            data-testid="poll-day"
            :data-date="day"
            :closable-label="t('poll.removeDay')"
            @click:close="removeDay(day)"
          >
            {{ formatDay(day) }}
          </v-chip>
        </div>

        <v-alert v-if="errorText" type="error" class="mb-4" role="alert" data-testid="poll-error">
          {{ errorText }}
        </v-alert>

        <v-btn
          type="submit"
          color="primary"
          size="large"
          :loading="busy"
          data-testid="poll-submit"
        >
          {{ t('poll.submit') }}
        </v-btn>
      </v-form>

      <div v-if="polls.created" data-testid="poll-created" class="mt-6">
        <ShareLink
          :path="`/u/${polls.created.participantToken}`"
          :label="t('poll.shareLink')"
          :hint="`${t('poll.retention')}: ${retentionText}`"
          hint-testid="poll-retention"
          link-testid="poll-share-link"
        />
      </div>
    </v-card-text>
  </v-card>
</template>
