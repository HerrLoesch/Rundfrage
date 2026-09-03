<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'

const props = withDefaults(
  defineProps<{
    path: string
    label: string
    hint?: string
    linkTestid?: string
    hintTestid?: string
  }>(),
  { linkTestid: 'share-url', hintTestid: 'share-hint' },
)

const { t } = useI18n()
const url = computed(() => `${window.location.origin}${props.path}`)
const copied = ref(false)

async function copy() {
  try {
    await navigator.clipboard.writeText(url.value)
    copied.value = true
    setTimeout(() => (copied.value = false), 2000)
  } catch {
    // Clipboard access can be refused; the link is visible and selectable either way.
  }
}
</script>

<template>
  <v-alert type="success" data-testid="share-link" class="share">
    <div class="text-subtitle-2 font-weight-bold mb-1">{{ props.label }}</div>

    <div class="d-flex align-center ga-2 flex-wrap">
      <code class="url flex-grow-1" :data-testid="props.linkTestid">{{ url }}</code>
      <v-btn
        size="small"
        variant="tonal"
        :prepend-icon="copied ? 'mdi-check' : 'mdi-content-copy'"
        data-testid="share-copy"
        @click="copy"
      >
        {{ copied ? t('share.copied') : t('share.copy') }}
      </v-btn>
    </div>

    <div v-if="props.hint" class="text-body-2 mt-2" :data-testid="props.hintTestid">
      {{ props.hint }}
    </div>
  </v-alert>
</template>

<style scoped>
.url { word-break: break-all; font-family: ui-monospace, monospace; }
</style>
