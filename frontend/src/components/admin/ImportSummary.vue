<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ImportSummary } from '../../api/client'
import ShareLink from '../poll/ShareLink.vue'

const props = defineProps<{ summary: ImportSummary }>()

const { t } = useI18n()

/**
 * Skip reasons are codes; the words live in the locale (002 FR-029). Rendering the code itself
 * would put `already_expired` in front of an operator, which is not a sentence.
 */
const skipped = computed(() =>
  props.summary.skipped.map((s, index) => ({
    key: `${s.kind}-${s.reason}-${index}`,
    text: t(`import.skip.${s.reason}`, { detail: s.detail ?? '' }),
  })),
)
</script>

<template>
  <!--
    Focusable and announced, so the outcome reaches a screen reader rather than being rendered
    silently below the fold (ui-contract §4).

    "Nichts übernommen" is deliberately NOT an error alert. A file holding one expired poll
    produces exactly that outcome, and FR-004 requires it to read as a plain statement of what
    happened - the same register as a success with skips, not the red of a refusal.
  -->
  <v-alert
    :type="props.summary.imported ? 'success' : 'info'"
    tabindex="-1"
    role="status"
    data-testid="import-summary"
    class="mt-4"
  >
    <div class="text-subtitle-2 font-weight-bold mb-2">{{ t('import.summaryTitle') }}</div>

    <p v-if="!props.summary.imported" class="mb-0" data-testid="import-nothing">
      {{ t('import.nothingTaken') }}
    </p>

    <p v-else class="mb-0" data-testid="import-counts">
      {{
        t('import.taken', {
          days: props.summary.counts.days,
          responses: props.summary.counts.responses,
        })
      }}
    </p>

    <ShareLink
      v-if="props.summary.participantToken"
      class="mt-3"
      :path="`/u/${props.summary.participantToken}`"
      :label="t('import.newLink')"
      :hint="t('import.newLinkHint')"
      link-testid="import-link"
      hint-testid="import-link-hint"
    />

    <!--
      Absent when empty rather than an empty heading: a "Nicht übernommen" section with nothing
      under it reads as a defect the operator then goes looking for.
    -->
    <div v-if="skipped.length > 0" class="mt-3" data-testid="import-skipped">
      <div class="text-subtitle-2 font-weight-bold mb-1">{{ t('import.skippedTitle') }}</div>
      <ul class="ps-4">
        <li v-for="item in skipped" :key="item.key">{{ item.text }}</li>
      </ul>
    </div>
  </v-alert>
</template>
