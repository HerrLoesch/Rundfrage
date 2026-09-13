<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import wordmark from '../../assets/rundfrage-logo.svg'

const { t } = useI18n()
</script>

<template>
  <!--
    The surface with no navigation: the participant views and the sign-in form.

    A poll link leads to a poll and nothing else, so nothing here invites a participant towards
    an area they cannot enter (Principle I, 007 FR-009). The sign-in form shares this chrome for
    the same reason from the other direction - there is no session yet, so there are no areas to
    list (007 FR-008).
  -->
  <v-app-bar :elevation="1" color="surface" density="comfortable">
    <!--
      A surface-coloured bar rather than a coloured block, because the wordmark brings its own
      two colours and would have to be flattened to sit on top of one.

      Placed in the bar's own content rather than in v-app-bar-title. That component is built for
      text: it is a block with a line-height and an ellipsis for overflow, so an image inside it
      sits on the text baseline - four pixels above the middle of the bar, measured. Neither the
      line-height nor the ellipsis does anything for a logo. The bar's content is a centred flex
      row, which is exactly what a logo needs.
    -->
    <RouterLink to="/" class="brand" data-testid="brand">
      <!--
        Width and height are stated so the bar does not reflow the moment the file arrives, and
        the alt text comes from the translations: it is the accessible name of this link, and
        without it the link is announced as "graphic" and leads nowhere a screen reader can
        describe.
      -->
      <img :src="wordmark" :alt="t('app.title')" width="139" height="36" />
    </RouterLink>
  </v-app-bar>

  <v-main>
    <RouterView />
  </v-main>
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
