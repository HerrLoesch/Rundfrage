<script setup lang="ts">
import { computed, ref } from 'vue'
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

/**
 * Folded on arrival, on both surfaces (FR-003).
 *
 * Deliberately a plain ref and not a store: the state must not outlive the component, because
 * carrying it across visits would mean remembering something about an anonymous reader (FR-008).
 * Folding is also purely local - nothing is requested and nothing is written (FR-007).
 */
const summaryOpen = ref(false)

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

      <!--
        Directly above the table it reveals. A disclosure is understood by proximity: its content
        appears immediately beneath it (research R-4).

        No aria-controls. The attribute does accept a list of ids, but the rows it would name do
        not exist while folded, and a reference to nothing is worse than an omitted optional
        attribute - aria-expanded is the part assistive technology acts on (research R-2).
      -->
      <div v-else class="mb-2">
        <v-btn
          variant="text"
          size="small"
          :prepend-icon="summaryOpen ? 'mdi-chevron-up' : 'mdi-chevron-down'"
          :aria-expanded="summaryOpen ? 'true' : 'false'"
          data-testid="summary-toggle"
          @click="summaryOpen = !summaryOpen"
        >
          {{ t('results.summary') }}
          <span class="d-sr-only">
            {{ summaryOpen ? t('results.summaryHide') : t('results.summaryShow') }}
          </span>
        </v-btn>
      </div>

      <!-- FR-036c: 100 day columns have to scroll inside the table, not the page body. -->
      <v-table v-if="poll.responseCount > 0" density="comfortable" class="scroller">
        <thead>
          <!--
            The summary lives inside this table on purpose. Each day's counts stay over their own
            column when a hundred days scroll sideways because the table does that - it is not
            something implemented here and could not be, if the summary sat outside the table
            (FR-013, research R-1).

            Rendered only while unfolded rather than hidden by a style: absent content cannot be
            announced, focused, or un-hidden by a later rule, a print stylesheet or a browser
            setting (FR-006, research R-3).
          -->
          <template v-if="summaryOpen">
            <tr
              v-for="state in TOTAL_ROWS"
              :key="state"
              data-testid="summary-row"
              :data-state="state"
              class="summary"
            >
              <!-- First cell, so it lands in the column that names the participants (FR-001a). -->
              <th scope="row" class="font-weight-bold">
                <v-icon
                  :icon="MARKS[state].icon"
                  :color="MARKS[state].colour"
                  size="small"
                  class="mr-1"
                  aria-hidden="true"
                />
                {{ t(`participate.${state}`) }}
              </th>
              <td v-for="day in poll.days" :key="day.id" class="text-center font-weight-bold">
                {{ totalsByDay[day.id]?.[state] ?? 0 }}
              </td>
              <td v-if="deletable"></td>
            </tr>
          </template>

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
      </v-table>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.scroller { overflow-x: auto; }

/*
  The three summary rows read as one block, so they carry one background and no rules between
  them. The rule has to be turned off explicitly: a header cell gets a bottom border and a data
  cell does not, which drew a line under the labels that stopped dead at the first number column.
*/
.summary { background: rgb(var(--v-theme-background)); }
.summary th,
.summary td { border-bottom: none !important; }

/*
  The label must not wrap. With a hundred day columns competing for width the name column
  narrows until "Vielleicht" breaks below its own mark - and the label beside the mark is the
  only thing that says which of the three stacked numbers is which (FR-001a). The table already
  scrolls inside itself, so a wider first column costs scrolling in the grid, not on the page.
*/
.summary th { white-space: nowrap; }
</style>
