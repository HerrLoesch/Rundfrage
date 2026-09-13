<script setup lang="ts">
import { computed, ref, useId } from 'vue'
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

/** Unique per instance, so two of these on one page describe their own link and not each other. */
const noteId = useId()
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
  <!--
    A panel, not a success alert.

    It used to be `v-alert type="success"`, which was wrong in two ways at once. On the wish-list
    detail page nothing had just succeeded - the link is a standing property of the list, and a
    green congratulation sat there permanently. And where something *had* succeeded, the caller
    wrapped this in its own success alert, so a green box appeared inside a green box.

    Neutral here; the caller says whether the occasion is a happy one.
  -->
  <section class="rf-share" data-testid="share-link">
    <div class="text-overline text-medium-emphasis mb-1">{{ props.label }}</div>

    <div class="d-flex align-center ga-2 flex-wrap">
      <!--
        An anchor, not a code element. The text content stays the bare address - people select
        and paste these far more often than they click them (FR-017).

        The new-tab note sits *outside* the link and is referenced as its description (FR-016a).
        Inside the link it would be part of the link's text, and the address would silently stop
        being the address - which is precisely what happened when it was first written that way,
        and what eleven end-to-end tests then said about it.

        rel denies the opened page any handle on this one (FR-016b).
      -->
      <a
        class="rf-address flex-grow-1"
        :href="url"
        target="_blank"
        rel="noopener noreferrer"
        :aria-describedby="noteId"
        :data-testid="props.linkTestid"
        >{{ url }}</a
      >
      <span :id="noteId" class="d-sr-only">{{ t('share.newTab') }}</span>
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

    <p v-if="props.hint" class="text-body-2 text-medium-emphasis mt-2 mb-0" :data-testid="props.hintTestid">
      {{ props.hint }}
    </p>
  </section>
</template>

<style scoped>
.rf-share {
  padding: 16px;
  border: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  background: rgb(var(--v-theme-background));
}
</style>
