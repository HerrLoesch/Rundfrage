<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMaintenanceStore } from '../../stores/maintenance'

const { t } = useI18n()
const store = useMaintenanceStore()

const confirming = ref(false)

// The warning banner used to live here, below the switch. It belongs to the shell now (007
// FR-026a), and it had to move for this control to move: the documented failure mode of
// maintenance mode is forgetting to switch it back off, so the warning has to be where the
// operator cannot avoid it - whereas this control now sits on the settings page, which is the one
// place they are certain to be looking when they already remember (research.md R-6).

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
