<script setup lang="ts">
import { useI18n } from 'vue-i18n'

/**
 * The one destructive confirmation in the admin area.
 *
 * Feature 009 generalised it rather than adding a second: two confirmation components would be two
 * places for "are you sure" to drift apart in weight and wording, and 009 FR-020b needs the
 * *distinction between two actions* to be obvious, which is easier when they are drawn by the same
 * hand. The poll case keeps its exact previous behaviour through the defaults.
 *
 * `heading`, `body` and `confirmLabel` are already-translated text, not keys. The caller knows
 * which numbers belong in its sentence - a poll states its responses, an Ersteller states both its
 * polls and its wish lists - and threading every shape through one key would make the catalogue
 * entry unreadable.
 */
const props = withDefaults(
  defineProps<{
    title: string
    responseCount?: number
    heading?: string | null
    body?: string | null
    /** A second line under the body, for what the operator should do *before* confirming. */
    note?: string | null
    confirmLabel?: string | null
    /** Test id for the surrounding card, so two confirmations can be told apart in a test. */
    testid?: string
  }>(),
  {
    responseCount: 0,
    heading: null,
    body: null,
    note: null,
    confirmLabel: null,
    testid: 'delete-confirm',
  },
)

const emit = defineEmits<{ confirm: []; cancel: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog :model-value="true" max-width="520" persistent @update:model-value="emit('cancel')">
    <v-card :data-testid="props.testid" role="alertdialog">
      <v-card-item>
        <template #prepend>
          <v-icon icon="mdi-alert-circle-outline" color="error" size="large" />
        </template>
        <v-card-title tag="h3">{{ props.heading ?? t('delete.confirmTitle') }}</v-card-title>
      </v-card-item>

      <v-card-text :data-testid="`${props.testid}-body`">
        <!-- FR-038: what will be destroyed, stated in numbers before it happens. -->
        {{ props.body ?? t('delete.confirmBody', { title: props.title, count: props.responseCount }) }}

        <!-- 009 FR-020c: the content cannot be kept by handing it to the operator, so the only way
             to keep anything is to have exported it first. Said here, where it still helps. -->
        <p v-if="props.note" class="mt-3 text-medium-emphasis" :data-testid="`${props.testid}-note`">
          {{ props.note }}
        </p>
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" data-testid="delete-cancel" @click="emit('cancel')">
          {{ t('delete.cancel') }}
        </v-btn>
        <v-btn color="error" data-testid="delete-confirm-button" @click="emit('confirm')">
          {{ props.confirmLabel ?? t('delete.confirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
