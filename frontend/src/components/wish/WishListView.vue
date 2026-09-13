<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  claimWishItems,
  fetchWishListByToken,
  type ApiProblem,
  type ParticipantWishList,
} from '../../api/client'
import { useProblemText } from '../../composables/useProblemText'
import ShareLink from '../poll/ShareLink.vue'
import MaintenanceNotice from '../poll/MaintenanceNotice.vue'

const props = defineProps<{ listToken: string }>()

const { t } = useI18n()
const problemText = useProblemText()

const list = ref<ParticipantWishList | null>(null)
const chosen = ref<Set<string>>(new Set())
const displayName = ref('')
const claimToken = ref<string | null>(null)
const problem = ref<ApiProblem | null>(null)
const loading = ref(true)
const busy = ref(false)
const notFound = ref(false)
const maintenance = ref(false)

/**
 * Nothing stands between the link and the form (Principle I, FR-014): the page loads the list and
 * the entry fields together, and there is no step, no sign-in and no confirmation before them.
 */
onMounted(load)

async function load() {
  loading.value = true
  try {
    list.value = await fetchWishListByToken(props.listToken)
  } catch (failure) {
    const code = (failure as ApiProblem).code
    // One wording for unknown, malformed and deleted alike, because the server gives one answer
    // (FR-024, SC-012).
    notFound.value = code === 'not_found'
    maintenance.value = code === 'maintenance'
  } finally {
    loading.value = false
  }
}

function toggle(itemId: string) {
  const next = new Set(chosen.value)
  if (next.has(itemId)) next.delete(itemId)
  else next.add(itemId)
  chosen.value = next
}

async function submit() {
  problem.value = null

  if (chosen.value.size === 0) {
    problem.value = { code: 'nothing_chosen' }
    return
  }

  busy.value = true
  try {
    // One submission for every item chosen: one request, one personal link, one of the ten
    // permitted per hour (FR-016, FR-022b, FR-023).
    const accepted = await claimWishItems(props.listToken, displayName.value, [...chosen.value])
    claimToken.value = accepted.claimToken
    // The list as the claim left it, so the open places update without a manual reload (FR-021).
    list.value = accepted.list
    chosen.value = new Set()
    displayName.value = ''
  } catch (failure) {
    problem.value = failure as ApiProblem
    // A refusal is judged on the server's current state, so re-read rather than leave the page
    // showing what it believed a moment ago (FR-017b, FR-028e).
    await load()
  } finally {
    busy.value = false
  }
}

const refusal = computed(() =>
  problem.value?.code === 'nothing_chosen' ? t('claim.nothingChosen') : problemText(problem.value),
)
</script>

<template>
  <v-container class="rf-page rf-page--participant">
    <div v-if="loading" class="text-center py-12" data-testid="wish-loading">
      <v-progress-circular indeterminate color="primary" />
      <p class="mt-4 text-body-1">{{ t('wish.loading') }}</p>
    </div>

    <!-- Before the not-found branch: during maintenance nothing is looked up, so a real list and
         an invented one must reach the same notice (FR-025). -->
    <MaintenanceNotice v-else-if="maintenance" />

    <v-alert v-else-if="notFound" type="warning" data-testid="wish-not-found">
      {{ t('wish.gone') }}
    </v-alert>

    <template v-else-if="list">
      <header class="rf-section-gap">
        <h1 class="rf-title text-h4" data-testid="wish-view-title">{{ list.title }}</h1>
        <p v-if="list.description" class="text-body-1 mt-2" data-testid="wish-view-description">
          {{ list.description }}
        </p>
        <p class="rf-meta text-medium-emphasis mt-2" data-testid="wish-view-target">
          <span class="rf-meta__item">
            <v-icon icon="mdi-calendar" size="16" />
            {{ t('wish.targetDate') }}: {{ list.targetDate }}
          </span>
        </p>
      </header>

      <!-- The list is closed: everything it showed is still shown, and the form is honestly
           absent rather than present and refusing (FR-028b). -->
      <v-alert v-if="list.closed" type="info" class="rf-section-gap" data-testid="wish-closed">
        {{ t('wish.closedSince', { date: list.targetDate }) }}
      </v-alert>

      <v-alert v-if="claimToken" type="success" class="rf-section-gap" data-testid="wish-submitted">
        <p class="mb-2">{{ t('claim.submitted') }}</p>
        <ShareLink
          :path="`/z/${claimToken}`"
          :label="t('claim.personalLink')"
          :hint="t('claim.personalHint')"
          link-testid="claim-url"
          hint-testid="claim-hint"
        />
      </v-alert>

      <!--
        The name first, then what they bring.

        The two used to be the other way round: a participant ticked boxes and then scrolled past
        the whole list to find a name field at the bottom. Asking who you are before asking what
        you will bring is the order the sentence has, and the order the operator asks it in.

        Still one page and one form. FR-013 puts everything on the page a link opens, and FR-014
        forbids any step between the link and the entry - so this is a reordering, never a wizard.
        The wrapper is a `form` while the list is open and a plain `div` once it has closed, which
        keeps the items below rendered exactly once for both states (FR-028b).
      -->
      <component
        :is="list.closed ? 'div' : 'form'"
        :data-testid="list.closed ? undefined : 'wish-claim-form'"
        @submit.prevent="submit"
      >
        <section v-if="!list.closed" class="rf-step">
          <h2 class="rf-step__head">
            <span class="rf-step__number" aria-hidden="true">1</span>
            <span class="rf-title text-h5">{{ t('claim.stepName') }}</span>
          </h2>

          <!-- Said before the name field, not after it (FR-019a). -->
          <p class="text-body-2 text-medium-emphasis rf-heading-gap" data-testid="wish-visibility-notice">
            {{ t('claim.visibilityNotice') }}
          </p>

          <v-text-field
            v-model="displayName"
            class="rf-step__name"
            :label="t('claim.name')"
            maxlength="100"
            data-testid="wish-name"
          />
        </section>

        <section class="rf-step">
          <h2 v-if="!list.closed" class="rf-step__head">
            <span class="rf-step__number" aria-hidden="true">2</span>
            <span class="rf-title text-h5">{{ t('claim.title') }}</span>
          </h2>

          <p v-if="list.items.length === 0" class="text-body-1" data-testid="wish-no-items">
            {{ t('wish.noItems') }}
          </p>

          <!--
            A bordered card like every other block on the page, rather than a bare v-list whose
            white strip ran edge to edge with nothing containing it.
          -->
          <v-card v-else data-testid="wish-items">
            <div
              v-for="item in list.items"
              :key="item.id"
              class="rf-wish-item"
              :data-testid="`wish-item-${item.id}`"
            >
              <!-- Selection is a checkbox rather than a coloured row: the state has to be readable
                   without colour (FR-057) and operable by keyboard (FR-056).

                   The slot keeps its width for a *full* item, so its text starts on the same line
                   as a claimable one's. On a closed list nothing is claimable at all, so the
                   column has nothing to align and is dropped rather than left as a dead indent. -->
              <div v-if="!list.closed" class="rf-wish-item__choose">
                <v-checkbox-btn
                  v-if="!list.closed && item.openPlaces > 0"
                  :model-value="chosen.has(item.id)"
                  :aria-label="`${t('claim.choose')}: ${item.name}`"
                  :data-testid="`wish-choose-${item.id}`"
                  @update:model-value="toggle(item.id)"
                />
              </div>

              <div class="rf-wish-item__text">
                <div class="text-subtitle-1 font-weight-medium">{{ item.name }}</div>

                <div class="text-body-2 text-medium-emphasis">
                  <span v-if="item.openPlaces > 0" :data-testid="`wish-open-${item.id}`">
                    {{ t('wish.openPlaces', { count: item.openPlaces, wanted: item.wantedCount }) }}
                  </span>
                  <!-- In words, not by colour (FR-018, FR-057). -->
                  <span
                    v-else
                    class="font-weight-medium text-success"
                    :data-testid="`wish-complete-${item.id}`"
                  >
                    {{ t('wish.complete') }}
                  </span>
                </div>

                <!-- The names on their own line. Run together with the count behind an em dash
                     they read as one sentence, and a long list pushed the count off the row. -->
                <div v-if="item.names.length" class="rf-wish-item__names mt-1">
                  <v-chip v-for="name in item.names" :key="name" variant="tonal">{{ name }}</v-chip>
                </div>
                <div v-else class="text-body-2 text-disabled mt-1">{{ t('wish.noNames') }}</div>
              </div>
            </div>
          </v-card>
        </section>

        <div v-if="!list.closed" class="rf-step__submit">
          <v-alert v-if="problem" type="error" class="rf-heading-gap" data-testid="wish-problem">
            {{ refusal }}
          </v-alert>

          <v-btn
            type="submit"
            color="primary"
            size="large"
            :loading="busy"
            data-testid="wish-submit"
          >
            {{ t('claim.submit') }}
          </v-btn>
        </div>
      </component>

    </template>
  </v-container>
</template>

<style scoped>
/*
 * A numbered step. The number is decorative - `aria-hidden`, because the heading text already
 * says what the step asks for and a screen reader announcing "one Wie heißt du" reads worse than
 * the heading alone. The order is carried by the document, which is the thing that actually has
 * to be right.
 */
.rf-step + .rf-step { margin-block-start: var(--rf-section); }

.rf-step__head {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-block-end: var(--rf-heading);
}

.rf-step__number {
  flex: 0 0 auto;
  display: grid;
  place-items: center;
  inline-size: 28px;
  block-size: 28px;
  border-radius: 50%;
  background: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-on-primary));
  font-size: 0.875rem;
  font-weight: 600;
  line-height: 1;
}

/* A name is short; a field spanning the whole column invites an essay. */
.rf-step__name { max-width: 24rem; }

/* The action that ends the form, set off from the last step above it. */
.rf-step__submit { margin-block-start: var(--rf-section); }

.rf-wish-item {
  display: flex;
  align-items: flex-start;
  gap: 4px;
  padding: 16px var(--rf-card-pad);
}

.rf-wish-item + .rf-wish-item {
  border-block-start: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

/* Reserved whether or not a checkbox is drawn, so every item's text starts at one x. */
.rf-wish-item__choose {
  flex: 0 0 40px;
  display: flex;
  justify-content: center;
  padding-block-start: 2px;
}

.rf-wish-item__text { flex: 1 1 auto; min-width: 0; }

.rf-wish-item__names {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
</style>
