<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAnsweringStore } from '../../stores/answering'
import { useProblemText } from '../../composables/useProblemText'
import { parseDateOnly } from '../../dates'
import type { Availability } from '../../api/client'

const props = defineProps<{ mode: 'submit' | 'revise'; busy?: boolean }>()

/**
 * Declared deliberately.
 *
 * Without it, Vue treats the parent's `@submit` as a fallthrough attribute and binds it as a
 * *native* listener on this component's root form - in addition to delivering the emitted
 * event. The handler ran twice and every answer was submitted twice, which only showed up as a
 * second row in the grid.
 */
const emit = defineEmits<{ submit: [] }>()

const { t, d } = useI18n()
const store = useAnsweringStore()
const problemText = useProblemText()

const STATES: { value: Availability; colour: string; icon: string }[] = [
  { value: 'yes', colour: 'state-yes', icon: 'mdi-check' },
  { value: 'maybe', colour: 'state-maybe', icon: 'mdi-help' },
  { value: 'no', colour: 'state-no', icon: 'mdi-close' },
]

const errorText = computed(() => problemText(store.problem))

function formatDay(date: string): string {
  return d(parseDateOnly(date), 'long')
}
</script>

<template>
  <v-form data-testid="answer-form" @submit.prevent="emit('submit')">
    <!--
      The same numbered-step pattern the wish-list claim form uses (FR-056), so the two
      participant surfaces read alike: who you are, then what you're asked, then the action
      that sends it.
    -->
    <section class="rf-step">
      <h2 class="rf-step__head">
        <span class="rf-step__number" aria-hidden="true">1</span>
        <span class="rf-title text-h5">{{ t('participate.stepName') }}</span>
      </h2>

      <!--
        FR-036a: this stands BEFORE the name field on purpose. Nobody should discover only
        after submitting that their name is visible to everyone holding the link.
      -->
      <v-alert
        type="info"
        density="compact"
        class="rf-heading-gap"
        data-testid="visibility-notice"
      >
        {{ t('participate.visibilityNotice') }}
      </v-alert>

      <v-text-field
        :model-value="store.displayName"
        :label="t('participate.name')"
        data-testid="participant-name"
        maxlength="100"
        prepend-inner-icon="mdi-account-outline"
        class="rf-step__name"
        @update:model-value="store.displayName = $event"
      />
    </section>

    <section class="rf-step">
      <h2 class="rf-step__head">
        <span class="rf-step__number" aria-hidden="true">2</span>
        <span class="rf-title text-h5">{{ t('participate.stepAvailability') }}</span>
      </h2>

      <!--
        One radio group per day. Vuetify renders native radio inputs, so keyboard operation,
        labelling and the focus ring remain the platform's job (FR-050 to FR-052,
        research.md R-11). Selecting nothing is the *no answer* state - it needs no control.
      -->
      <v-sheet
        v-for="day in store.poll?.days ?? []"
        :key="day.id"
        border
        rounded
        class="pa-3 mb-3"
        data-testid="day-choice"
        :data-day-id="day.id"
      >
        <v-radio-group
          :model-value="store.answers[day.id] ?? null"
          :label="formatDay(day.date)"
          inline
          hide-details
          @update:model-value="store.setAnswer(day.id, $event as Availability)"
        >
          <v-radio
            v-for="state in STATES"
            :key="state.value"
            :value="state.value"
            :color="state.colour"
            :data-testid="`choice-${state.value}`"
          >
            <template #label>
              <v-icon :icon="state.icon" size="small" class="mr-1" />
              {{ t(`participate.${state.value}`) }}
            </template>
          </v-radio>
        </v-radio-group>
      </v-sheet>

      <v-alert v-if="errorText" type="error" class="mt-4" role="alert" data-testid="answer-error">
        {{ errorText }}
      </v-alert>
    </section>

    <section class="rf-step">
      <h2 class="rf-step__head">
        <span class="rf-step__number" aria-hidden="true">3</span>
        <span class="rf-title text-h5">{{ t('participate.stepSubmit') }}</span>
      </h2>

      <v-btn
        type="submit"
        color="primary"
        variant="flat"
        size="large"
        :loading="props.busy"
        data-testid="answer-submit"
      >
        {{ props.mode === 'revise' ? t('participate.save') : t('participate.submit') }}
      </v-btn>
    </section>
  </v-form>
</template>
