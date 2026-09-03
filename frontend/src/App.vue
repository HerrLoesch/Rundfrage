<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const route = useRoute()

// The participant surface carries no navigation: a poll link leads to a poll and nothing else,
// so nothing invites a participant towards an admin area they cannot use (Principle I).
const showAdminChrome = computed(() => route.path.startsWith('/admin'))
</script>

<template>
  <v-app>
    <v-app-bar :elevation="1" color="primary" density="comfortable">
      <v-app-bar-title>
        <RouterLink :to="showAdminChrome ? '/admin' : '/'" class="brand">
          {{ t('app.title') }}
        </RouterLink>
      </v-app-bar-title>
    </v-app-bar>

    <v-main>
      <RouterView />
    </v-main>
  </v-app>
</template>

<style scoped>
.brand { color: inherit; text-decoration: none; font-weight: 600; }
</style>
