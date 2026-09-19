<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  fetchClaims,
  withdrawClaim,
  type ApiProblem,
  type ClaimEntry,
  type ClaimGroup,
} from '../../api/client'
import { useProblemText } from '../../composables/useProblemText'
import { parseDateOnly } from '../../dates'
import MaintenanceNotice from '../poll/MaintenanceNotice.vue'

const props = defineProps<{ claimToken: string }>()

const { t, d } = useI18n()
const problemText = useProblemText()

const group = ref<ClaimGroup | null>(null)
const loading = ref(true)
const notFound = ref(false)
const maintenance = ref(false)
const problem = ref<ApiProblem | null>(null)
const withdrawing = ref<ClaimEntry | null>(null)
const withdrawn = ref(false)

/**
 * This link covers exactly the entries of the submission that minted it, and grants nothing else:
 * no other entry, no other list, no admin function (FR-022c).
 */
onMounted(load)

async function load() {
  loading.value = true
  try {
    group.value = await fetchClaims(props.claimToken)
  } catch (failure) {
    const code = (failure as ApiProblem).code
    // Withdrawing the last entry this link covers leaves the server with nothing to answer, and
    // it answers the neutral 404 (FR-022e). That is correct, and it is not "your link is
    // unknown" - so the page keeps what it is showing and lets the emptied branch below say the
    // entries are gone. Treating it as not-found would replace the confirmation the participant
    // just earned with a warning that their link never existed.
    notFound.value = code === 'not_found' && !withdrawn.value
    if (code === 'not_found' && withdrawn.value) {
      group.value = group.value === null ? null : { ...group.value, entries: [] }
    }
    maintenance.value = code === 'maintenance'
  } finally {
    loading.value = false
  }
}

async function confirmWithdrawal() {
  const entry = withdrawing.value
  if (!entry) return

  problem.value = null
  try {
    await withdrawClaim(props.claimToken, entry.claimId)
    withdrawn.value = true
    withdrawing.value = null
    await load()
  } catch (failure) {
    problem.value = failure as ApiProblem
    withdrawing.value = null
  }
}

/** `group.targetDate` is a bare `DateOnly` string; formatted here rather than at each call site. */
const targetDateText = computed(() =>
  group.value ? d(parseDateOnly(group.value.targetDate), 'long') : '',
)
</script>

<template>
  <v-container class="rf-page rf-page--participant">
    <div v-if="loading" class="text-center py-12" data-testid="claim-loading">
      <v-progress-circular indeterminate color="primary" />
      <p class="mt-4 text-body-1">{{ t('wish.loading') }}</p>
    </div>

    <MaintenanceNotice v-else-if="maintenance" />

    <!-- Unknown, malformed, or every covered entry already gone: one wording (FR-022e). -->
    <v-alert v-else-if="notFound" type="warning" data-testid="claim-not-found">
      {{ t('claim.notFound') }}
    </v-alert>

    <template v-else-if="group">
      <!-- text-h4, like every other page's title. This page alone used text-h5 and 720px, so
           following a personal link from the wish list shrank both the heading and the column. -->
      <header class="rf-section-gap">
        <h1 class="rf-title text-h4" data-testid="claim-list-title">{{ group.listTitle }}</h1>
        <p class="rf-meta text-medium-emphasis mt-2">
          <span class="rf-meta__item">
            <v-icon icon="mdi-calendar" size="16" />
            {{ t('wish.targetDate') }}: {{ targetDateText }}
          </span>
        </p>
      </header>

      <!-- Closed: the entries stay readable, and withdrawal is gone rather than present and
           refusing, because a place freed now can no longer be claimed (FR-028d). -->
      <v-alert v-if="group.closed" type="info" class="rf-section-gap" data-testid="claim-closed">
        {{ t('wish.closedSince', { date: targetDateText }) }}
      </v-alert>

      <v-alert v-if="withdrawn" type="success" class="mb-4" data-testid="claim-withdrawn">
        {{ t('claim.withdrawn') }}
      </v-alert>

      <v-alert v-if="problem" type="error" class="mb-4" data-testid="claim-problem">
        {{ problemText(problem) }}
      </v-alert>

      <h2 class="rf-title text-h5 rf-heading-gap">{{ t('claim.myEntries') }}</h2>

      <!-- Reachable once every entry has been withdrawn: the link is spent, and saying so is
           kinder than an empty list with no explanation (FR-022a). -->
      <v-alert
        v-if="group.entries.length === 0"
        type="info"
        data-testid="claim-entries-empty"
      >
        {{ t('claim.allWithdrawn') }}
      </v-alert>

      <v-card v-else data-testid="claim-entries">
        <div
          v-for="entry in group.entries"
          :key="entry.claimId"
          class="rf-claim-row"
          :data-testid="`claim-entry-${entry.claimId}`"
        >
          <div class="rf-claim-row__text">
            <div class="text-subtitle-1 font-weight-medium">{{ entry.itemName }}</div>
            <div class="text-body-2 text-medium-emphasis">{{ entry.displayName }}</div>
          </div>

          <!-- Beside its own entry rather than pushed to the far edge by an append slot. -->
          <v-btn
            v-if="!group.closed"
            variant="text"
            color="error"
            prepend-icon="mdi-close"
            :data-testid="`claim-withdraw-${entry.claimId}`"
            @click="withdrawing = entry"
          >
            {{ t('claim.withdraw') }}
          </v-btn>
        </div>
      </v-card>

      <p class="text-body-2 text-medium-emphasis rf-section" data-testid="claim-lost-link">
        {{ t('claim.lostLink') }}
      </p>

      <v-dialog :model-value="withdrawing !== null" @update:model-value="withdrawing = null">
        <v-card v-if="withdrawing" data-testid="claim-withdraw-confirm">
          <v-card-title>{{ t('claim.withdrawConfirmTitle') }}</v-card-title>
          <v-card-text>
            {{ t('claim.withdrawConfirmBody', {
              item: withdrawing.itemName,
              name: withdrawing.displayName,
            }) }}
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="withdrawing = null">{{ t('wish.cancel') }}</v-btn>
            <v-btn color="error" data-testid="claim-withdraw-confirmed" @click="confirmWithdrawal">
              {{ t('claim.withdraw') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
    </template>
  </v-container>
</template>

<style scoped>
/* Identical to the item rows on the wish-list page - the same kind of list on the same surface. */
.rf-claim-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px var(--rf-card-pad);
}

.rf-claim-row + .rf-claim-row {
  border-block-start: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.rf-claim-row__text { min-width: 0; }
</style>
