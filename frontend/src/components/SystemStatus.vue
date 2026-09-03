<script setup lang="ts">
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useStatusStore } from '../stores/status'

// The walking-skeleton diagnostic from feature 001. It still proves the whole chain, it is just
// no longer the front door - see /status.
const { t } = useI18n()
const store = useStatusStore()

const COLOURS: Record<string, string> = {
  loading: 'info',
  reachable: 'success',
  unreachable: 'error',
  backendUnreachable: 'warning',
}

const ICONS: Record<string, string> = {
  loading: 'mdi-timer-sand',
  reachable: 'mdi-database-check',
  unreachable: 'mdi-database-off',
  backendUnreachable: 'mdi-server-off',
}

onMounted(() => {
  void store.load()
})
</script>

<template>
  <v-container max-width="720" class="py-8">
    <h1 class="text-h4 mb-1" data-testid="app-title">{{ t('app.title') }}</h1>
    <p class="text-subtitle-1 mb-6">{{ t('app.subtitle') }}</p>

    <v-card class="mb-4">
      <v-card-item>
        <v-card-title tag="h2" class="text-subtitle-1">{{ t('message.label') }}</v-card-title>
      </v-card-item>
      <v-card-text data-testid="backend-message">
        {{ store.message ?? t('message.unavailable') }}
      </v-card-text>
    </v-card>

    <v-card>
      <v-card-item>
        <v-card-title tag="h2" class="text-subtitle-1">{{ t('status.label') }}</v-card-title>
      </v-card-item>
      <v-card-text>
        <!-- The state is carried by text and an icon, never by colour alone. -->
        <v-alert
          :type="COLOURS[store.databaseState] as 'success' | 'error' | 'warning' | 'info'"
          :icon="ICONS[store.databaseState]"
          data-testid="database-state"
          :data-state="store.databaseState"
        >
          {{ t(store.databaseStateKey) }}
        </v-alert>
      </v-card-text>
    </v-card>
  </v-container>
</template>
