<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useDashboardStore } from '../../stores/dashboard'
import { useWishListsStore, filledPercent } from '../../stores/wishLists'
import { useMaintenanceStore } from '../../stores/maintenance'
import { useSessionStore } from '../../stores/session'
import PageHeader from '../layout/PageHeader.vue'

const { t, d } = useI18n()
const router = useRouter()
const dashboard = useDashboardStore()
/**
 * The same store, and therefore the same read, the wish-list area uses (008 research R-5). One
 * projection with two readers is what makes FR-049's agreement structural rather than something a
 * test has to keep true.
 */
const wishLists = useWishListsStore()
const maintenance = useMaintenanceStore()
const session = useSessionStore()

/**
 * Re-read on entering the area (FR-030). The router keeps no area alive, so returning here
 * remounts this view - which means a poll deleted elsewhere is reflected without a watcher and
 * without a manual reload.
 */
onMounted(async () => {
  // Two reads, because these are two independent sets of figures. One can fail while the other
  // succeeds, which is why each region carries its own empty and unreachable state (008 FR-052).
  await Promise.all([dashboard.load(), wishLists.load()])

  if (dashboard.loadProblem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
  }
})

/**
 * The three states that must never look alike.
 *
 * `unreachable` renders no figure at all: a partly filled dashboard makes claims about data the
 * system has just said it cannot read (FR-032). `empty` says so in words rather than printing
 * six zeros (FR-031). Only `ready` shows numbers.
 */
const unreachable = computed(
  () => dashboard.loadProblem !== null && dashboard.loadProblem.code !== 'unauthorized',
)
const empty = computed(() => dashboard.figures?.pollCount === 0)

/**
 * Whether anybody has answered at all. Distinct from a genuine zero in one column: three zeros
 * among no answers is not a finding, and printing them as one would be (FR-028 scenario 8).
 */
const nothingAnswered = computed(() => {
  const f = dashboard.figures
  return f !== null && f.yes + f.maybe + f.no === 0
})

const nextDeletionText = computed(() => {
  const next = dashboard.figures?.nextDeletion
  return next ? t('dashboard.deletionsHint', { moment: d(new Date(next), 'long') }) : null
})

/** 008 FR-052: the wish-list region's own three states, independent of the poll figures. */
const wishListsUnreachable = computed(
  () => wishLists.loadProblem !== null && wishLists.loadProblem.code !== 'unauthorized',
)

const sinceText = computed(() =>
  maintenance.since
    ? t('maintenance.since', { moment: d(new Date(maintenance.since), 'long') })
    : null,
)
</script>

<template>
  <v-container class="rf-page rf-page--admin" data-testid="dashboard">
    <PageHeader :title="t('dashboard.title')" />

    <!--
      Placeholders, never numbers. SC-011 allows up to two seconds, and two seconds of zeroed
      tiles is a dashboard asserting something false about the data.
    -->
    <div v-if="dashboard.loading && !dashboard.figures" data-testid="dashboard-loading">
      <v-progress-linear indeterminate class="mb-4" />
      <p class="text-body-2">{{ t('dashboard.loading') }}</p>
    </div>

    <v-alert v-else-if="unreachable" type="warning" data-testid="storage-unavailable">
      {{ t('storage.unavailable') }}
    </v-alert>

    <v-alert v-else-if="empty" type="info" data-testid="dashboard-empty">
      {{ t('dashboard.empty') }}
    </v-alert>

    <!--
      Reached only once loading, unreachable and empty have all been ruled out by the chain above,
      so the figures are present by construction. A `ready` computed once stood here as well and
      decided nothing - the branch it guarded was already unreachable without it.
    -->
    <template v-else-if="dashboard.figures">
      <!--
        `align-stretch` and a full-height card, so the four tiles end on one line.

        Without it each tile was exactly as tall as its own text, and the fourth - which carries a
        date and therefore three lines - stood 24px lower than the first. Four boxes whose bottoms
        disagree is the single loudest thing on a dashboard, and no amount of spacing elsewhere
        makes up for it.
      -->
      <v-row class="align-stretch">
        <v-col cols="12" sm="6" md="3">
          <v-card class="h-100" data-testid="stat-polls">
            <v-card-item class="rf-tile">
              <div class="text-overline text-medium-emphasis">{{ t('dashboard.polls') }}</div>
              <div class="text-h3 rf-figure">{{ dashboard.figures.pollCount }}</div>
              <div class="text-body-2 text-medium-emphasis">{{ t('dashboard.pollsHint') }}</div>
            </v-card-item>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card class="h-100" data-testid="stat-responses">
            <v-card-item class="rf-tile">
              <div class="text-overline text-medium-emphasis">{{ t('dashboard.responses') }}</div>
              <div class="text-h3 rf-figure">{{ dashboard.figures.responseCount }}</div>
              <div class="text-body-2 text-medium-emphasis">{{ t('dashboard.responsesHint') }}</div>
            </v-card-item>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card class="h-100" data-testid="stat-unanswered">
            <v-card-item class="rf-tile">
              <div class="text-overline text-medium-emphasis">{{ t('dashboard.unanswered') }}</div>
              <div class="text-h3 rf-figure">{{ dashboard.figures.unansweredPolls }}</div>
              <div class="text-body-2 text-medium-emphasis">{{ t('dashboard.unansweredHint') }}</div>
            </v-card-item>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card class="h-100" data-testid="stat-deletions">
            <v-card-item class="rf-tile">
              <div class="text-overline text-medium-emphasis">{{ t('dashboard.deletions') }}</div>
              <div class="text-h3 rf-figure">{{ dashboard.figures.deletionsDueSoon }}</div>
              <!--
                Two readings, kept apart (data-model.md, figure 4). "Nothing is due this week" is
                not "there is nothing" - so when a date exists it is named even at a count of
                zero, and only a system with no polls at all says nothing about a next deletion.
              -->
              <div
                v-if="nextDeletionText"
                class="text-body-2 text-medium-emphasis"
                data-testid="stat-next-deletion"
              >
                {{ nextDeletionText }}
              </div>
              <div v-else class="text-body-2 text-medium-emphasis">
                {{ t('dashboard.deletionsNone') }}
              </div>
            </v-card-item>
          </v-card>
        </v-col>
      </v-row>

      <v-row class="align-stretch mt-1">
        <v-col cols="12" md="6">
          <v-card class="h-100" data-testid="stat-distribution">
            <v-card-item class="rf-tile">
              <div class="text-overline text-medium-emphasis">{{ t('dashboard.distribution') }}</div>

              <p v-if="nothingAnswered" class="text-body-2 mt-2" data-testid="distribution-empty">
                {{ t('dashboard.distributionEmpty') }}
              </p>

              <!-- Three figures on one baseline, each under its own label, all three the same
                   size - the reading is a comparison and a comparison needs equal terms. -->
              <div v-else class="rf-split mt-2">
                <div>
                  <div class="text-overline text-medium-emphasis">{{ t('results.totalYes') }}</div>
                  <div class="text-h4 rf-figure" data-testid="distribution-yes">
                    {{ dashboard.figures.yes }}
                  </div>
                </div>
                <div>
                  <div class="text-overline text-medium-emphasis">{{ t('results.totalMaybe') }}</div>
                  <div class="text-h4 rf-figure" data-testid="distribution-maybe">
                    {{ dashboard.figures.maybe }}
                  </div>
                </div>
                <div>
                  <div class="text-overline text-medium-emphasis">{{ t('results.totalNo') }}</div>
                  <div class="text-h4 rf-figure" data-testid="distribution-no">
                    {{ dashboard.figures.no }}
                  </div>
                </div>
              </div>
            </v-card-item>
          </v-card>
        </v-col>

        <v-col cols="12" md="6">
          <!--
            Figure 5, read from the maintenance store rather than from the dashboard payload. The
            shell needs that store for the banner anyway, and one source is what keeps the banner
            and this tile from contradicting each other on the same screen (FR-029).
          -->
          <v-card class="h-100" data-testid="stat-maintenance">
            <v-card-item class="rf-tile">
              <div class="text-overline text-medium-emphasis">{{ t('dashboard.maintenance') }}</div>
              <div class="text-h5">
                {{ maintenance.enabled ? t('maintenance.on') : t('maintenance.off') }}
              </div>
              <div
                v-if="sinceText"
                class="text-body-2 text-medium-emphasis"
                data-testid="stat-maintenance-since"
              >
                {{ sinceText }}
              </div>
            </v-card-item>
          </v-card>
        </v-col>
      </v-row>
    </template>

    <!--
      The wish-list overview (008 FR-048a), outside the poll chain above on purpose: an
      installation with no polls and three wish lists must not be told that nothing is stored.

      Titles appear here and participant names never do. That is the whole of the amendment 008
      FR-048b made to 007 FR-034: a list title is written by the operator, for the operator, and
      names nobody.
    -->
    <section class="rf-section" data-testid="dashboard-wish-lists">
      <div class="d-flex align-center flex-wrap ga-3 rf-heading-gap">
        <h2 class="rf-title text-h5">{{ t('dashboard.wishLists') }}</h2>
        <span
          v-if="!wishListsUnreachable && wishLists.lists.length > 0"
          class="text-body-2 text-medium-emphasis"
          data-testid="dashboard-wish-lists-total"
        >
          {{ t('dashboard.wishListsHint', { count: wishLists.lists.length }) }}
        </span>
      </div>

      <v-alert
        v-if="wishListsUnreachable"
        type="warning"
        data-testid="dashboard-wish-lists-unavailable"
      >
        {{ t('storage.unavailable') }}
      </v-alert>

      <v-alert
        v-else-if="!wishLists.loading && wishLists.lists.length === 0"
        type="info"
        data-testid="dashboard-wish-lists-empty"
      >
        {{ t('dashboard.wishListsEmpty') }}
      </v-alert>

      <!--
        Bounded and scrolling within itself: at the 200 lists FR-054 documents, an unbounded table
        would push every figure above it off the screen (FR-048c). The order is the API's and is
        not re-sorted here.
      -->
      <v-card v-else>
        <div class="rf-table-scroll">
          <v-table density="compact">
            <thead>
              <tr>
                <th>{{ t('dashboard.wishListsColumnTitle') }}</th>
                <th class="text-no-wrap">{{ t('dashboard.wishListsColumnDate') }}</th>
                <th class="text-no-wrap">{{ t('dashboard.wishListsColumnState') }}</th>
                <th class="text-end text-no-wrap">{{ t('dashboard.wishListsColumnEntries') }}</th>
                <th class="text-end text-no-wrap">{{ t('dashboard.wishListsColumnFilled') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="list in wishLists.lists"
                :key="list.id"
                data-testid="dashboard-wish-list-row"
              >
                <td>
                  <!--
                    A link into the area, which is navigation rather than a control: the dashboard
                    reports and does not act (FR-048d, FR-051).
                  -->
                  <RouterLink
                    class="rf-link"
                    :to="{ name: 'wish-list', params: { wishListId: list.id } }"
                    :data-testid="`dashboard-wish-link-${list.id}`"
                  >
                    {{ list.title }}
                  </RouterLink>
                </td>
                <td class="text-no-wrap">{{ list.targetDate }}</td>
                <td class="text-no-wrap">{{ list.closed ? t('wish.closed') : t('wish.open') }}</td>
                <td class="text-end rf-figure text-no-wrap">
                  {{ list.entryCount }} / {{ list.placeCount }}
                </td>
                <td class="text-end rf-figure text-no-wrap">
                  {{ filledPercent(list.entryCount, list.placeCount) }} %
                </td>
              </tr>
            </tbody>
          </v-table>
        </div>
      </v-card>
    </section>
  </v-container>
</template>
