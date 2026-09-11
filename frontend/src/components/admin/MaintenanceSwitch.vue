<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMaintenanceStore } from '../../stores/maintenance'

const { t, d } = useI18n()
const store = useMaintenanceStore()

const confirming = ref(false)

const sinceText = computed(() =>
  store.since ? t('maintenance.since', { moment: d(new Date(store.since), 'long') }) : null,
)

/**
 * Asymmetric on purpose (ui-contract §2). Switching *on* takes participants away, so it asks
 * first. Switching *off* gives them back, and a confirmation there would put one more step
 * between the operator and the end of an outage.
 */
function toggle() {
  if (store.enabled) {
    void store.set(false)
    return
  }

  confirming.value = true
}

function confirmOn() {
  confirming.value = false
  void store.set(true)
}
</script>

<template>
  <div>
    <v-btn
      variant="outlined"
      :color="store.enabled ? 'warning' : undefined"
      :prepend-icon="store.enabled ? 'mdi-wrench' : 'mdi-wrench-outline'"
      :loading="store.busy"
      data-testid="maintenance-toggle"
      @click="toggle"
    >
      {{ t('maintenance.switch') }}:
      {{ store.enabled ? t('maintenance.on') : t('maintenance.off') }}
    </v-btn>

    <!--
      A banner, not a toast. A toast is missed, and the failure mode of this feature is leaving
      the participant side switched off after the work is finished.
    -->
    <v-alert
      v-if="store.enabled"
      type="warning"
      class="mt-4"
      data-testid="maintenance-banner"
    >
      <div class="text-subtitle-2 font-weight-bold">{{ t('maintenance.bannerTitle') }}</div>
      <div class="text-body-2">{{ t('maintenance.bannerBody') }}</div>
      <div v-if="sinceText" class="text-body-2 mt-1" data-testid="maintenance-since">
        {{ sinceText }}
      </div>
    </v-alert>

    <!--
      An inline confirmation rather than a dialog. The operator is already looking at this
      control, so a modal adds a layer without adding clarity - and the one dialog this project
      has (DeleteConfirm) carries no unit test, which is not a precedent worth copying for the
      control that starts an outage.
    -->
    <v-card v-if="confirming" class="mt-4" variant="tonal" data-testid="maintenance-confirm">
      <v-card-title class="text-subtitle-1">{{ t('maintenance.confirmOnTitle') }}</v-card-title>
      <v-card-text>{{ t('maintenance.confirmOnBody') }}</v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" data-testid="maintenance-cancel" @click="confirming = false">
          {{ t('maintenance.cancel') }}
        </v-btn>
        <v-btn color="warning" data-testid="maintenance-confirm-on" @click="confirmOn">
          {{ t('maintenance.confirmOn') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </div>
</template>
