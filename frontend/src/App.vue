<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import wordmark from '../assets/rundfrage-logo.svg'

const { t } = useI18n()
const route = useRoute()

// The participant surface carries no navigation: a poll link leads to a poll and nothing else,
// so nothing invites a participant towards an admin area they cannot use (Principle I).
const showAdminChrome = computed(() => route.path.startsWith('/admin'))
</script>

<template>
  <v-app>
    <!--
      A surface-coloured bar rather than a coloured block, because the wordmark brings its own
      two colours and would have to be flattened to sit on top of one. The rule is the same as
      for the results grid: the design carries the meaning, and we do not recolour it to fit.
    -->
    <v-app-bar :elevation="1" color="surface" density="comfortable">
      <!--
        Placed in the bar's own content rather than in v-app-bar-title. That component is built
        for text: it is a block with a line-height and an ellipsis for overflow, so an image
        inside it sits on the text baseline - four pixels above the middle of the bar, measured.
        Neither the line-height nor the ellipsis does anything for a logo. The bar's content is
        a centred flex row, which is exactly what a logo needs.
      -->
      <RouterLink
        :to="showAdminChrome ? '/admin' : '/'"
        class="brand"
        data-testid="brand"
      >
        <!--
          Width and height are stated so the bar does not reflow the moment the file arrives,
          and the alt text comes from the translations: it is the accessible name of this link,
          and without it the link is announced as "graphic" and leads nowhere a screen reader
          can describe.
        -->
        <img :src="wordmark" :alt="t('app.title')" width="139" height="36" />
      </RouterLink>
    </v-app-bar>

    <v-main>
      <RouterView />
    </v-main>
  </v-app>
</template>

<style scoped>
.brand {
  display: flex;
  align-items: center;
  text-decoration: none;
  /* The indent v-app-bar-title used to provide. */
  margin-inline-start: 20px;
}

.brand img { display: block; }
</style>
