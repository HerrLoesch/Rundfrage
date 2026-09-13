<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useDisplay } from 'vuetify'

const { t } = useI18n()
const { mdAndUp } = useDisplay()
const route = useRoute()

const open = defineModel<boolean>('open', { default: false })

/**
 * Whether the drawer is showing.
 *
 * `permanent` does not override v-model - a permanent drawer whose model is false is still
 * closed, which left the entries in the DOM but unreachable (Playwright saw tabindex="-2" and
 * waited for a visible element that never came). So above the breakpoint this is simply true, and
 * below it the app bar's toggle governs.
 */
const visible = computed({
  get: () => (mdAndUp.value ? true : open.value),
  set: (value: boolean) => {
    open.value = value
  },
})

/**
 * The whole navigation, as a list rather than as data (Principle III).
 *
 * Four entries, so there is still no registry, no configuration and no plugin point. FR-003 fixes
 * the order - dashboard first, feature areas in the middle, settings last - and FR-004 forbids an
 * entry for anything unbuilt. Feature 008 added the second middle entry by adding a line here,
 * which is what the shape was for.
 *
 * "Terminfindungen" reuses poll.listTitle rather than adding a second word for the poll list
 * (FR-002a): two catalogue entries for one concept is how a rename ends up half-applied.
 */
/**
 * Each entry names the routes it owns, and that is what marks it current.
 *
 * Route-based matching cannot express this on its own, in two opposite ways. Inclusive matching
 * marked /admin as current on every address beneath it, so two entries carried aria-current at
 * once (FR-002 forbids exactly that). Exact matching then left the poll entry unmarked while one
 * poll's answers were shown - because `poll-answers` is a *sibling* record of `polls`, not a
 * descendant, and Vue Router derives "active" from record ancestry rather than from path
 * prefixes. FR-014e requires the area to stay marked there.
 *
 * Naming the routes is therefore not a shortcut around the router; it is the only statement that
 * is true in both cases.
 *
 * Both the styling (`active`) and the semantics (`aria-current`) are driven from this one list,
 * because they had to be: v-list-item does not derive aria-current from `active` - it comes from
 * the underlying link's own inclusive matching, which is what produced both wrong answers above.
 * Setting the attribute explicitly overrides it, so there is exactly one source for what
 * assistive technology reads, and that source is the route name. This corrects research.md R-2,
 * which assumed the router alone would get this right.
 */
const entries = computed(() => [
  {
    to: '/admin',
    owns: ['dashboard'],
    label: t('nav.dashboard'),
    icon: 'mdi-view-dashboard-outline',
    testid: 'nav-dashboard',
  },
  {
    to: '/admin/terminfindungen',
    owns: ['polls', 'poll-answers'],
    label: t('poll.listTitle'),
    icon: 'mdi-calendar-multiselect',
    testid: 'nav-polls',
  },
  {
    // Owns both of its route names, so the area stays marked current while one list's detail is
    // shown - the same correction the poll entry records above (008 FR-041).
    to: '/admin/wunschlisten',
    owns: ['wish-lists', 'wish-list'],
    label: t('nav.wishLists'),
    icon: 'mdi-gift-outline',
    testid: 'nav-wish-lists',
  },
  {
    to: '/admin/einstellungen',
    owns: ['settings'],
    label: t('nav.settings'),
    icon: 'mdi-cog-outline',
    testid: 'nav-settings',
  },
])

const isCurrent = (owns: string[]) => owns.includes(String(route.name))

/**
 * Permanent where there is room beside the content, temporary where there is not. A temporary
 * drawer closes when an entry is chosen, which is what FR-012 asks for: choosing a destination
 * must reveal it rather than leave it covered.
 */
function chosen() {
  if (!mdAndUp.value) open.value = false
}
</script>

<template>
  <v-navigation-drawer
    v-model="visible"
    :permanent="mdAndUp"
    :temporary="!mdAndUp"
    data-testid="admin-nav"
  >
    <!--
      `nav` gives the region a landmark, so the navigation is reachable as one (FR-013). The list
      items render RouterLinks and Vuetify emits aria-current="page" from `active` - the attribute
      assistive technology actually reads. `active` is driven by the route name alone (see the
      comment on `entries`), so there is still exactly one source for which entry is current.
    -->
    <v-list nav :aria-label="t('nav.label')">
      <v-list-item
        v-for="entry in entries"
        :key="entry.to"
        :to="entry.to"
        :active="isCurrent(entry.owns)"
        :aria-current="isCurrent(entry.owns) ? 'page' : undefined"
        :prepend-icon="entry.icon"
        :title="entry.label"
        :data-testid="entry.testid"
        @click="chosen"
      />
    </v-list>
  </v-navigation-drawer>
</template>
