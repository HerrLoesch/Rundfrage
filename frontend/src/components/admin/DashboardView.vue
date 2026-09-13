<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useDashboardStore } from '../../stores/dashboard'
import { useMaintenanceStore } from '../../stores/maintenance'
import { useSessionStore } from '../../stores/session'

const { t, d } = useI18n()
const router = useRouter()
const dashboard = useDashboardStore()
const maintenance = useMaintenanceStore()
const session = useSessionStore()

/**
 * Re-read on entering the area (FR-030). The router keeps no area alive, so returning here
 * remounts this view - which means a poll deleted elsewhere is reflected without a watcher and
 * without a manual reload.
 */
onMounted(async () => {
  await dashboard.load()

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

const sinceText = computed(() =>
  maintenance.since
    ? t('maintenance.since', { moment: d(new Date(maintenance.since), 'long') })
    : null,
)
</script>

<template>
  <v-container max-width="1100" class="py-8" data-testid="dashboard">
    <h1 class="text-h4 mb-6">{{ t('dashboard.title') }}</h1>

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
      <v-row>
        <v-col cols="12" sm="6" md="3">
          <v-card data-testid="stat-polls">
            <v-card-item>
              <div class="text-overline">{{ t('dashboard.polls') }}</div>
              <div class="text-h3">{{ dashboard.figures.pollCount }}</div>
              <div class="text-body-2">{{ t('dashboard.pollsHint') }}</div>
            </v-card-item>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card data-testid="stat-responses">
            <v-card-item>
              <div class="text-overline">{{ t('dashboard.responses') }}</div>
              <div class="text-h3">{{ dashboard.figures.responseCount }}</div>
              <div class="text-body-2">{{ t('dashboard.responsesHint') }}</div>
            </v-card-item>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card data-testid="stat-unanswered">
            <v-card-item>
              <div class="text-overline">{{ t('dashboard.unanswered') }}</div>
              <div class="text-h3">{{ dashboard.figures.unansweredPolls }}</div>
              <div class="text-body-2">{{ t('dashboard.unansweredHint') }}</div>
            </v-card-item>
          </v-card>
        </v-col>

        <v-col cols="12" sm="6" md="3">
          <v-card data-testid="stat-deletions">
            <v-card-item>
              <div class="text-overline">{{ t('dashboard.deletions') }}</div>
              <div class="text-h3">{{ dashboard.figures.deletionsDueSoon }}</div>
              <!--
                Two readings, kept apart (data-model.md, figure 4). "Nothing is due this week" is
                not "there is nothing" - so when a date exists it is named even at a count of
                zero, and only a system with no polls at all says nothing about a next deletion.
              -->
              <div v-if="nextDeletionText" class="text-body-2" data-testid="stat-next-deletion">
                {{ nextDeletionText }}
              </div>
              <div v-else class="text-body-2">{{ t('dashboard.deletionsNone') }}</div>
            </v-card-item>
          </v-card>
        </v-col>
      </v-row>

      <v-row>
        <v-col cols="12" md="6">
          <v-card data-testid="stat-distribution">
            <v-card-item>
              <div class="text-overline">{{ t('dashboard.distribution') }}</div>
            </v-card-item>
            <v-card-text>
              <p v-if="nothingAnswered" data-testid="distribution-empty">
                {{ t('dashboard.distributionEmpty') }}
              </p>
              <div v-else class="d-flex ga-6">
                <div>
                  <div class="text-overline">{{ t('results.totalYes') }}</div>
                  <div class="text-h4" data-testid="distribution-yes">
                    {{ dashboard.figures.yes }}
                  </div>
                </div>
                <div>
                  <div class="text-overline">{{ t('results.totalMaybe') }}</div>
                  <div class="text-h4" data-testid="distribution-maybe">
                    {{ dashboard.figures.maybe }}
                  </div>
                </div>
                <div>
                  <div class="text-overline">{{ t('results.totalNo') }}</div>
                  <div class="text-h4" data-testid="distribution-no">
                    {{ dashboard.figures.no }}
                  </div>
                </div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>

        <v-col cols="12" md="6">
          <!--
            Figure 5, read from the maintenance store rather than from the dashboard payload. The
            shell needs that store for the banner anyway, and one source is what keeps the banner
            and this tile from contradicting each other on the same screen (FR-029).
          -->
          <v-card data-testid="stat-maintenance">
            <v-card-item>
              <div class="text-overline">{{ t('dashboard.maintenance') }}</div>
              <div class="text-h5">
                {{ maintenance.enabled ? t('maintenance.on') : t('maintenance.off') }}
              </div>
              <div v-if="sinceText" class="text-body-2" data-testid="stat-maintenance-since">
                {{ sinceText }}
              </div>
            </v-card-item>
          </v-card>
        </v-col>
      </v-row>
    </template>
  </v-container>
</template>
