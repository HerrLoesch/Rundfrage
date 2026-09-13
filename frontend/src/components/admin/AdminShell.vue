<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import wordmark from '../../../assets/rundfrage-logo.svg'
import AdminNav from './AdminNav.vue'
import { useSessionStore } from '../../stores/session'
import { useMaintenanceStore } from '../../stores/maintenance'

const { t, d } = useI18n()
const router = useRouter()
const session = useSessionStore()
const maintenance = useMaintenanceStore()

/**
 * Read once, here, for the whole admin area.
 *
 * The banner below and the dashboard's maintenance figure are two views of one state, and
 * FR-029 requires every figure to agree with what its area shows. Loading it in the shell gives
 * both of them the same source; loading it per area would give them two.
 *
 * It also carries FR-011 for every area at once. Each area that loads something of its own reacts
 * to a refused session itself, but settings loads nothing - so opening it without a session drew
 * the whole shell, navigation and all, around a page the server would refuse. Asking here means
 * the refusal is caught wherever the operator entered, including areas added later that happen to
 * need no data.
 */
onMounted(async () => {
  await maintenance.load()

  if (maintenance.problem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
  }
})

const sinceText = computed(() =>
  maintenance.since
    ? t('maintenance.since', { moment: d(new Date(maintenance.since), 'long') })
    : null,
)

/**
 * Only meaningful below the breakpoint where the drawer is permanent. Held here rather than in
 * AdminNav because the control that flips it lives in the app bar, which is the shell's.
 */
const drawerOpen = ref(false)

async function signOut() {
  await session.signOut()
  await router.push({ name: 'sign-in' })
}
</script>

<template>
  <div data-testid="admin-shell">
    <v-app-bar :elevation="1" color="surface" density="comfortable">
      <!--
        The drawer's toggle, and only below the breakpoint where the drawer stops being permanent.
        Above it the control would open what is already open (FR-012).
      -->
      <v-app-bar-nav-icon
        class="d-md-none"
        :aria-label="t('nav.toggle')"
        data-testid="admin-nav-toggle"
        @click="drawerOpen = !drawerOpen"
      />

      <!--
        Same reasoning as the participant bar this replaces: the wordmark brings its own two
        colours and sits in the bar's centred flex row rather than in v-app-bar-title, which is
        built for text and would put an image on a text baseline.
      -->
      <RouterLink to="/admin" class="brand" data-testid="brand">
        <img :src="wordmark" :alt="t('app.title')" width="139" height="36" />
      </RouterLink>

      <v-spacer />

      <v-btn
        variant="text"
        prepend-icon="mdi-logout"
        data-testid="sign-out"
        @click="signOut"
      >
        {{ t('shell.signOut') }}
      </v-btn>
    </v-app-bar>

    <AdminNav v-model:open="drawerOpen" />

    <v-main>
      <!--
        The banner lives here rather than beside the switch that sets it (FR-026a).
        Its old home was inside MaintenanceSwitch, which meant the warning would have followed the
        switch to the settings page - out of every area where forgetting it is the actual risk.
        The documented failure mode of maintenance mode is leaving it on, so the warning belongs
        where the operator cannot be without seeing it (research.md R-6).
      -->
      <v-alert
        v-if="maintenance.enabled"
        type="warning"
        rounded="0"
        data-testid="maintenance-banner"
      >
        <div class="text-subtitle-2 font-weight-bold">{{ t('maintenance.bannerTitle') }}</div>
        <div class="text-body-2">{{ t('maintenance.bannerBody') }}</div>
        <div v-if="sinceText" class="text-body-2 mt-1" data-testid="maintenance-since">
          {{ sinceText }}
        </div>
      </v-alert>

      <RouterView />
    </v-main>
  </div>
</template>

<style scoped>
.brand {
  display: flex;
  align-items: center;
  text-decoration: none;
  margin-inline-start: 8px;
}

.brand img { display: block; }
</style>
