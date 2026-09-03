<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAnsweringStore } from '../../stores/answering'
import AnswerForm from './AnswerForm.vue'
import ResultGrid from './ResultGrid.vue'
import ShareLink from './ShareLink.vue'

const props = defineProps<{ pollToken?: string; editToken?: string }>()

const { t } = useI18n()
const store = useAnsweringStore()
const busy = ref(false)

/**
 * Revising, as soon as an answer exists - whether it arrived through a personal link or was
 * just submitted on this page.
 *
 * Previously this looked only at the route parameter, so after submitting, the form stayed in
 * "submit" mode with the name and answers still filled in. Pressing the button again recorded
 * a second response. The token received on submission is exactly the capability to revise, so
 * no identification is needed - which is what Principle I demands.
 */
const mode = computed<'submit' | 'revise'>(() =>
  props.editToken || store.editToken ? 'revise' : 'submit',
)

onMounted(async () => {
  if (props.editToken) await store.loadOwnResponse(props.editToken)
  else if (props.pollToken) await store.loadPoll(props.pollToken)
})

async function save() {
  busy.value = true
  try {
    if (mode.value === 'revise') await store.revise()
    else if (props.pollToken) await store.submit(props.pollToken)
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <v-container max-width="960" class="py-8">
    <div v-if="store.loading" class="text-center py-12" data-testid="poll-loading">
      <v-progress-circular indeterminate color="primary" />
      <p class="mt-4 text-body-1">{{ t('participate.loading') }}</p>
    </div>

    <!-- One message for all four causes, because the server gives one answer (SC-012). -->
    <v-alert v-else-if="store.notFound" type="warning" data-testid="poll-not-found">
      {{ t('participate.notFound') }}
    </v-alert>

    <template v-else-if="store.poll">
      <h1 class="text-h4 mb-2" data-testid="poll-view-title">{{ store.poll.title }}</h1>
      <p v-if="store.poll.message" class="text-body-1 mb-6" data-testid="poll-view-message">
        {{ store.poll.message }}
      </p>

      <v-alert
        v-if="store.justSubmitted"
        type="success"
        class="mb-4"
        data-testid="submitted-confirmation"
      >
        {{ t('participate.submitted') }}
      </v-alert>

      <v-alert
        v-if="store.justRevised"
        type="success"
        class="mb-4"
        data-testid="revised-confirmation"
      >
        {{ t('participate.revised') }}
      </v-alert>

      <!-- FR-026: the only way back to this answer, so it is shown, not merely returned. -->
      <ShareLink
        v-if="store.justSubmitted && store.editToken"
        :path="`/a/${store.editToken}`"
        :label="t('participate.editHint')"
        class="mb-6"
      />

      <AnswerForm :mode="mode" :busy="busy" class="mb-6" @submit="save" />

      <!-- FR-036b: readable before answering, so someone can see where the group is first. -->
      <ResultGrid :poll="store.poll" />
    </template>
  </v-container>
</template>
