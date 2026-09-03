<script setup lang="ts">
import { useI18n } from 'vue-i18n'

const props = defineProps<{ title: string; responseCount: number }>()
const emit = defineEmits<{ confirm: []; cancel: [] }>()

const { t } = useI18n()
</script>

<template>
  <v-dialog :model-value="true" max-width="520" persistent @update:model-value="emit('cancel')">
    <v-card data-testid="delete-confirm" role="alertdialog">
      <v-card-item>
        <template #prepend>
          <v-icon icon="mdi-alert-circle-outline" color="error" size="large" />
        </template>
        <v-card-title tag="h3">{{ t('delete.confirmTitle') }}</v-card-title>
      </v-card-item>

      <v-card-text data-testid="delete-confirm-body">
        <!-- FR-038: the number of responses that will be destroyed, stated before it happens. -->
        {{ t('delete.confirmBody', { title: props.title, count: props.responseCount }) }}
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" data-testid="delete-cancel" @click="emit('cancel')">
          {{ t('delete.cancel') }}
        </v-btn>
        <v-btn color="error" data-testid="delete-confirm-button" @click="emit('confirm')">
          {{ t('delete.confirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
