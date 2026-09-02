<script setup lang="ts">
import { onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useStatusStore } from '../stores/status'

// No literal user-facing strings live in this component; everything resolves through the
// i18n layer (FR-029). Tests assert on data-testid and translation keys (FR-030).
const { t } = useI18n()
const store = useStatusStore()

onMounted(() => {
  void store.load()
})
</script>

<template>
  <section class="system-status">
    <h1 data-testid="app-title">{{ t('app.title') }}</h1>
    <p class="subtitle">{{ t('app.subtitle') }}</p>

    <div class="block">
      <p class="label">{{ t('message.label') }}</p>
      <p data-testid="backend-message">
        {{ store.message ?? t('message.unavailable') }}
      </p>
    </div>

    <div class="block">
      <p class="label">{{ t('status.label') }}</p>
      <!--
        data-state carries the machine-readable state so tests never depend on translated
        text, and the class drives the visual distinction FR-010 requires.
      -->
      <p
        data-testid="database-state"
        :data-state="store.databaseState"
        :class="['state', `state--${store.databaseState}`]"
      >
        {{ t(store.databaseStateKey) }}
      </p>
    </div>
  </section>
</template>

<style scoped>
.system-status {
  font-family: system-ui, sans-serif;
  padding: 2rem;
  max-width: 40rem;
}
.subtitle {
  color: #555;
  margin-top: -0.5rem;
}
.block {
  margin-top: 1.5rem;
}
.label {
  font-weight: 600;
  margin-bottom: 0.25rem;
}
.state {
  display: inline-block;
  padding: 0.35rem 0.75rem;
  border-radius: 0.25rem;
  font-weight: 600;
  border-left: 4px solid transparent;
}
.state--loading {
  background: #eceff1;
  color: #37474f;
  border-left-color: #90a4ae;
}
.state--reachable {
  background: #e6f4ea;
  color: #1b5e20;
  border-left-color: #2e7d32;
}
.state--unreachable {
  background: #fdecea;
  color: #b71c1c;
  border-left-color: #c62828;
}
.state--backendUnreachable {
  background: #fff4e5;
  color: #7a3e00;
  border-left-color: #ef6c00;
}
</style>
