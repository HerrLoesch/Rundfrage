<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import MaintenanceSwitch from './MaintenanceSwitch.vue'
import RestorePanel from './RestorePanel.vue'
import { backupUrl } from '../../api/client'

const { t } = useI18n()
</script>

<template>
  <v-container max-width="900" class="py-8">
    <h1 class="text-h4 mb-2">{{ t('settings.title') }}</h1>

    <!--
      Sections, not dialogs (FR-020).

      The request asked for both "Settings Dialoge" and a settings page, and the page won because
      the project had already decided this once: MaintenanceSwitch confirms inline, on the stated
      grounds that the operator is already looking at the control. A modal for the same kind of
      decision would contradict that.

      They are ordered by consequence, least destructive first, and the restore is last and set
      apart (FR-020a) - the operator who came to download a backup should not have to pass the
      control that replaces every poll on the way to it.
    -->
    <v-card class="mb-6" data-testid="settings-maintenance">
      <v-card-item>
        <v-card-title tag="h2" class="text-h6">{{ t('settings.maintenanceTitle') }}</v-card-title>
        <v-card-subtitle class="text-wrap">{{ t('settings.maintenanceBody') }}</v-card-subtitle>
      </v-card-item>
      <v-card-text>
        <!--
          The toggle only. The banner it used to carry now belongs to the shell, so the warning is
          on screen in every area rather than only on the page holding the switch (FR-026a).
        -->
        <MaintenanceSwitch />
      </v-card-text>
    </v-card>

    <v-card class="mb-6" data-testid="settings-backup">
      <v-card-item>
        <v-card-title tag="h2" class="text-h6">{{ t('settings.backupTitle') }}</v-card-title>
        <v-card-subtitle class="text-wrap">{{ t('settings.backupBody') }}</v-card-subtitle>
      </v-card-item>
      <v-card-text>
        <v-btn
          variant="outlined"
          prepend-icon="mdi-database-arrow-down-outline"
          :href="backupUrl"
          data-testid="download-backup"
        >
          {{ t('backup.download') }}
        </v-btn>
      </v-card-text>
    </v-card>

    <!--
      Last, and deliberately not looking like the section above it. Downloading a copy and
      replacing everything with one are neighbours here by necessity; the separation is what stops
      them being reachable by the same reflex (FR-020a, FR-025).
    -->
    <v-card
      class="settings-grave"
      variant="outlined"
      color="error"
      data-testid="settings-restore"
    >
      <v-card-item>
        <v-card-title tag="h2" class="text-h6">{{ t('settings.restoreTitle') }}</v-card-title>
        <v-card-subtitle class="text-wrap">{{ t('settings.restoreBody') }}</v-card-subtitle>
      </v-card-item>
      <v-card-text>
        <RestorePanel />
      </v-card-text>
    </v-card>
  </v-container>
</template>

<style scoped>
/* A visible gap above, so the destructive section reads as a place you arrive at rather than
   the next thing down the page. */
.settings-grave { margin-top: 3rem; }
</style>
