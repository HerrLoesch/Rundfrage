<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Availability, PollView } from '../../api/client'

const props = defineProps<{ poll: PollView; deletable?: boolean }>()
const emit = defineEmits<{ deleteResponse: [responseId: string] }>()

const { t, d } = useI18n()

/**
 * Every state carries an icon and a screen-reader label as well as a colour (FR-053), and the
 * unanswered cell has its own mark rather than being blank (FR-024a). The grid therefore stays
 * readable in greyscale and cannot confuse "no time" with "no answer".
 */
const MARKS: Record<Availability | 'none', { icon: string; colour: string }> = {
  yes: { icon: 'mdi-check-circle', colour: 'state-yes' },
  maybe: { icon: 'mdi-help-circle', colour: 'state-maybe' },
  no: { icon: 'mdi-close-circle', colour: 'state-no' },
  none: { icon: 'mdi-minus-circle-outline', colour: 'state-none' },
}

const TOTAL_ROWS: Availability[] = ['yes', 'maybe', 'no']

const totalsByDay = computed(() =>
  Object.fromEntries(props.poll.totals.map((total) => [total.dayId, total])),
)

function answerFor(responseId: string, dayId: string): Availability | 'none' {
  const row = props.poll.responses.find((r) => r.id === responseId)
  return row?.answers.find((a) => a.dayId === dayId)?.availability ?? 'none'
}

function labelFor(state: Availability | 'none'): string {
  return state === 'none' ? t('participate.noAnswer') : t(`participate.${state}`)
}

function formatDay(date: string): string {
  return d(new Date(`${date}T12:00:00`), 'short')
}
</script>

<template>
  <v-card data-testid="result-grid">
    <v-card-item>
      <v-card-title tag="h2">{{ t('results.title') }}</v-card-title>
      <template #append>
        <v-chip v-if="poll.responseCount > 0" size="small" data-testid="response-count">
          {{ t('results.responseCount', { count: poll.responseCount }) }}
        </v-chip>
      </template>
    </v-card-item>

    <v-card-text>
      <v-alert v-if="poll.responseCount === 0" type="info" data-testid="results-empty">
        {{ t('results.empty') }}
      </v-alert>

      <!-- FR-036c: 100 day columns have to scroll inside the table, not the page body. -->
      <v-table v-else density="comfortable" class="scroller">
        <thead>
          <tr>
            <th scope="col">{{ t('results.participant') }}</th>
            <th v-for="day in poll.days" :key="day.id" scope="col" class="text-center">
              {{ formatDay(day.date) }}
            </th>
            <th v-if="deletable" class="text-center">
              <span class="d-sr-only">{{ t('results.deleteResponse') }}</span>
            </th>
          </tr>
        </thead>

        <tbody>
          <tr v-for="row in poll.responses" :key="row.id" data-testid="result-row">
            <th scope="row" class="font-weight-medium">{{ row.displayName }}</th>
            <td
              v-for="day in poll.days"
              :key="day.id"
              class="text-center"
              data-testid="result-cell"
              :data-state="answerFor(row.id, day.id)"
            >
              <v-icon
                :icon="MARKS[answerFor(row.id, day.id)].icon"
                :color="MARKS[answerFor(row.id, day.id)].colour"
                aria-hidden="true"
              />
              <span class="d-sr-only">{{ labelFor(answerFor(row.id, day.id)) }}</span>
            </td>
            <td v-if="deletable" class="text-center">
              <v-btn
                icon="mdi-delete-outline"
                size="small"
                variant="text"
                color="error"
                :aria-label="t('results.deleteResponse')"
                data-testid="delete-response"
                @click="emit('deleteResponse', row.id)"
              />
            </td>
          </tr>
        </tbody>

        <tfoot>
          <tr v-for="state in TOTAL_ROWS" :key="state" data-testid="totals-row" :data-state="state">
            <th scope="row" class="font-weight-bold">
              <v-icon :icon="MARKS[state].icon" :color="MARKS[state].colour" size="small" class="mr-1" />
              {{ t(`participate.${state}`) }}
            </th>
            <td v-for="day in poll.days" :key="day.id" class="text-center font-weight-bold">
              {{ totalsByDay[day.id]?.[state] ?? 0 }}
            </td>
            <td v-if="deletable"></td>
          </tr>
        </tfoot>
      </v-table>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.scroller { overflow-x: auto; }
tfoot { background: rgb(var(--v-theme-background)); }
</style>
