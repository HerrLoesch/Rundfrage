<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
</script>

<template>
  <!--
    The only page a participant can reach while maintenance is on.

    It says nothing about which poll was asked for, and nothing about whether one exists behind
    that link (FR-032). Same words for a real token and an invented one - a notice that varied
    would leak exactly what 002 SC-012's neutral 404 was built to hide.

    No retry control: a reload is the retry, and a button implying progress would promise
    something this page cannot know.
  -->
  <v-alert
    type="info"
    role="status"
    prominent
    data-testid="maintenance-notice"
  >
    <h1 class="text-h5 mb-2">{{ t('maintenance.noticeTitle') }}</h1>
    <p class="mb-0">{{ t('maintenance.noticeBody') }}</p>
  </v-alert>
</template>
