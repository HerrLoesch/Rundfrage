<script setup lang="ts">
// Nothing but the Vuetify application root.
//
// The chrome used to live here behind a conditional on the path, which worked while there was
// one bar with one variation. There are now two surfaces with genuinely different chrome - the
// admin shell has a navigation bar, a sign-out and a maintenance banner; the participant surface
// must have none of those (Principle I, 007 FR-009) - so each draws its own in a layout route
// of its own. A conditional here would have had to grow a branch per difference.
</script>

<template>
  <v-app>
    <RouterView />
  </v-app>
</template>
