<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useCreatorStore } from '../../stores/creator'
import { filledPercent, isComplete } from '../../stores/wishLists'
import { creatorExportUrl } from '../../api/client'

/**
 * Everything an Ersteller reaches, on the one page their link leads to (009 FR-024 to FR-028g).
 *
 * <b>One address, and no child routes.</b> A poll's answers and a wish list's detail open as
 * component state here rather than as destinations of their own. Feature 007 made the opposite
 * choice for the admin area and was right to: an operator holding a session loses nothing by an
 * address. Here the address carries the credential, so every additional one is another place the
 * token is written down - in history, in a copied link, in a screenshot - and the holder has no
 * password to fall back on when one of them leaks (spec Q5, research R-9).
 *
 * The cost is real and accepted: there is no deep link to one poll's answers and the back button
 * does nothing useful inside this page. FR-028g is what pays for it - each list carries enough
 * summary to find something without opening it first.
 *
 * <b>No dashboard, and nothing summing across the two lists</b> (FR-028b). A person with two short
 * lists in front of them does not need a third page counting them.
 */
const props = defineProps<{ creatorToken: string }>()

const { t } = useI18n()
const store = useCreatorStore()

const newPollTitle = ref('')
const newPollDays = ref('')
const newListTitle = ref('')
const newListDate = ref('')
const newListItems = ref('')

onMounted(() => store.load(props.creatorToken))

/**
 * An unusable link and an unreachable store are different things, and only the first one is about
 * this link. A neutral 404 says nothing about which - deliberately (FR-008) - so the interface
 * says the one thing that is true either way.
 */
const linkUnusable = computed(() => store.loadProblem !== null)

const problemText = computed(() => {
  const problem = store.problem
  if (!problem) return null
  return t(`error.${problem.code}`, {
    limit: problem.limit ?? 0,
    detail: problem.detail ?? '',
    minutes: Math.ceil((problem.retryAfterSeconds ?? 0) / 60),
  })
})

function share(path: string, token: string): string {
  return `${window.location.origin}/${path}/${token}`
}

async function createPoll() {
  const days = newPollDays.value
    .split(',')
    .map((d) => d.trim())
    .filter(Boolean)

  if (await store.createPoll(newPollTitle.value, null, days)) {
    newPollTitle.value = ''
    newPollDays.value = ''
  }
}

async function createWishList() {
  const items = newListItems.value
    .split(',')
    .map((name) => name.trim())
    .filter(Boolean)
    .map((name) => ({ name, wantedCount: 1 }))

  if (await store.createWishList(newListTitle.value, null, newListDate.value, items)) {
    newListTitle.value = ''
    newListDate.value = ''
    newListItems.value = ''
  }
}
</script>

<template>
  <v-container class="rf-page">
    <v-alert
      v-if="linkUnusable"
      type="warning"
      data-testid="creator-link-unusable"
    >
      {{ t('error.not_found') }}
    </v-alert>

    <div v-else-if="store.loading" class="text-center py-8" data-testid="creator-surface-loading">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else>
      <!-- FR-007: which link this is, so the holder can tell. -->
      <h1 class="rf-title text-h4" data-testid="creator-greeting">
        {{ t('creator.surfaceGreeting', { name: store.name }) }}
      </h1>

      <!--
        FR-058, and it has to come before anything is shared rather than after.

        Whoever holds this link can do everything the holder can do. Somebody forwarding it in the
        belief that it is read-only is the failure mode the bearer-link design actually has, and
        saying so is the only mitigation that costs nothing.
      -->
      <v-alert
        type="info"
        variant="tonal"
        class="mt-4"
        data-testid="creator-link-warning"
      >
        {{ t('creator.surfaceWarning') }}
      </v-alert>

      <v-alert v-if="problemText" type="error" class="mt-4" data-testid="creator-problem">
        {{ problemText }}
      </v-alert>

      <!-- ===================== Terminfindungen ===================== -->
      <section class="rf-section-gap mt-8" data-testid="creator-polls">
        <div class="d-flex align-center flex-wrap ga-2">
          <h2 class="rf-title text-h5">{{ t('creator.surfacePolls') }}</h2>
          <v-spacer />
          <v-btn
            color="primary"
            prepend-icon="mdi-plus"
            data-testid="creator-new-poll"
            @click="store.reveal({ kind: 'new-poll' })"
          >
            {{ t('creator.surfaceNewPoll') }}
          </v-btn>
        </div>

        <v-card v-if="store.open.kind === 'new-poll'" class="mt-3" data-testid="creator-poll-form">
          <v-card-text>
            <v-text-field
              v-model="newPollTitle"
              :label="t('poll.title')"
              maxlength="300"
              data-testid="creator-poll-title"
            />
            <v-text-field
              v-model="newPollDays"
              :label="t('poll.days')"
              :placeholder="t('creator.surfaceDaysPlaceholder')"
              data-testid="creator-poll-days"
            />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="store.close()">{{ t('creator.cancel') }}</v-btn>
            <v-btn color="primary" data-testid="creator-poll-submit" @click="createPoll">
              {{ t('creator.surfaceNewPoll') }}
            </v-btn>
          </v-card-actions>
        </v-card>

        <v-alert
          v-if="store.polls.length === 0"
          type="info"
          class="mt-3"
          data-testid="creator-polls-empty"
        >
          {{ t('creator.surfacePollsEmpty') }}
        </v-alert>

        <v-card
          v-for="poll in store.polls"
          v-else
          :key="poll.id"
          class="rf-row mt-3"
          data-testid="creator-poll-row"
          :data-poll-id="poll.id"
        >
          <div class="rf-row__body">
            <div class="rf-row__main">
              <h3 class="text-subtitle-1 font-weight-medium">{{ poll.title }}</h3>
              <!-- FR-028g: enough on the row to find what you are looking for without opening
                   each one in turn, because there is no way to link back to one. -->
              <div class="rf-meta text-medium-emphasis mt-1">
                <span class="rf-meta__item">
                  <v-icon icon="mdi-account-multiple-outline" size="16" />
                  {{ poll.responseCount }}
                </span>
                <span class="rf-meta__item">
                  <v-icon icon="mdi-calendar" size="16" />
                  {{ poll.dayCount }}
                </span>
              </div>
              <a
                class="rf-address mt-2"
                :href="share('u', poll.participantToken)"
                target="_blank"
                rel="noopener noreferrer"
                :aria-describedby="`newtab-poll-${poll.id}`"
                >{{ share('u', poll.participantToken) }}</a
              >
              <span :id="`newtab-poll-${poll.id}`" class="d-sr-only">{{ t('share.newTab') }}</span>
            </div>
            <div class="rf-row__actions">
              <v-btn
                variant="text"
                size="small"
                data-testid="creator-open-poll"
                @click="store.openPoll(poll.id)"
              >
                {{ t('results.title') }}
              </v-btn>
              <!-- FR-028: the same export the operator gets. There is deliberately no import
                   counterpart anywhere on this surface (FR-028a). -->
              <v-btn
                variant="text"
                size="small"
                :href="creatorExportUrl(store.token, poll.id)"
                data-testid="creator-export"
              >
                {{ t('creator.surfaceExport') }}
              </v-btn>
              <v-btn
                variant="text"
                size="small"
                color="error"
                data-testid="creator-delete-poll"
                @click="store.removePoll(poll.id)"
              >
                {{ t('delete.confirm') }}
              </v-btn>
            </div>
          </div>
        </v-card>

        <!-- Opened in place. The address does not change and no history entry is added
             (FR-028d, FR-028e). -->
        <v-card
          v-if="store.open.kind === 'poll' && store.pollView"
          class="mt-4"
          data-testid="creator-poll-answers"
        >
          <v-card-title tag="h3">{{ store.pollView.title }}</v-card-title>
          <v-card-text>
            <p class="text-medium-emphasis">{{ store.pollView.responseCount }}</p>
            <ul>
              <li v-for="response in store.pollView.responses" :key="response.id">
                {{ response.displayName }}
              </li>
            </ul>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" data-testid="creator-close-poll" @click="store.close()">
              {{ t('creator.surfaceClosed') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </section>

      <!-- ===================== Wunschlisten ===================== -->
      <section class="rf-section-gap mt-8" data-testid="creator-wish-lists">
        <div class="d-flex align-center flex-wrap ga-2">
          <h2 class="rf-title text-h5">{{ t('creator.surfaceWishLists') }}</h2>
          <v-spacer />
          <v-btn
            color="primary"
            prepend-icon="mdi-plus"
            data-testid="creator-new-wish-list"
            @click="store.reveal({ kind: 'new-wish-list' })"
          >
            {{ t('creator.surfaceNewWishList') }}
          </v-btn>
        </div>

        <v-card
          v-if="store.open.kind === 'new-wish-list'"
          class="mt-3"
          data-testid="creator-wish-list-form"
        >
          <v-card-text>
            <v-text-field
              v-model="newListTitle"
              :label="t('wish.title')"
              maxlength="300"
              data-testid="creator-wish-list-title"
            />
            <v-text-field
              v-model="newListDate"
              :label="t('wish.targetDate')"
              :placeholder="t('creator.surfaceDatePlaceholder')"
              data-testid="creator-wish-list-date"
            />
            <v-text-field
              v-model="newListItems"
              :label="t('wish.items')"
              :placeholder="t('creator.surfaceItemsPlaceholder')"
              data-testid="creator-wish-list-items"
            />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="store.close()">{{ t('creator.cancel') }}</v-btn>
            <v-btn color="primary" data-testid="creator-wish-list-submit" @click="createWishList">
              {{ t('creator.surfaceNewWishList') }}
            </v-btn>
          </v-card-actions>
        </v-card>

        <v-alert
          v-if="store.wishLists.length === 0"
          type="info"
          class="mt-3"
          data-testid="creator-wish-lists-empty"
        >
          {{ t('creator.surfaceWishListsEmpty') }}
        </v-alert>

        <v-card
          v-for="list in store.wishLists"
          v-else
          :key="list.id"
          class="rf-row mt-3"
          data-testid="creator-wish-list-row"
          :data-wish-list-id="list.id"
        >
          <div class="rf-row__body">
            <div class="rf-row__main">
              <div class="rf-row__title">
                <h3 class="text-subtitle-1 font-weight-medium">{{ list.title }}</h3>
                <!-- Closed and complete in words, never by colour alone (008 FR-057). -->
                <v-chip v-if="list.closed">{{ t('wish.closed') }}</v-chip>
                <v-chip
                  v-if="isComplete(list.entryCount, list.placeCount)"
                  color="success"
                  variant="tonal"
                >
                  {{ t('wish.complete') }}
                </v-chip>
              </div>
              <div class="rf-meta text-medium-emphasis mt-1">
                <span class="rf-meta__item">
                  <v-icon icon="mdi-calendar" size="16" />
                  {{ list.targetDate }}
                </span>
                <span class="rf-meta__item">
                  <v-icon icon="mdi-account-multiple-outline" size="16" />
                  {{ t('wish.filled', {
                    filled: list.entryCount,
                    places: list.placeCount,
                    percent: filledPercent(list.entryCount, list.placeCount),
                  }) }}
                </span>
              </div>
              <a
                class="rf-address mt-2"
                :href="share('w', list.listToken)"
                target="_blank"
                rel="noopener noreferrer"
                :aria-describedby="`newtab-wish-list-${list.id}`"
                >{{ share('w', list.listToken) }}</a
              >
              <span :id="`newtab-wish-list-${list.id}`" class="d-sr-only">{{ t('share.newTab') }}</span>
            </div>
            <div class="rf-row__actions">
              <v-btn
                variant="text"
                size="small"
                data-testid="creator-open-wish-list"
                @click="store.openWishList(list.id)"
              >
                {{ t('wish.open') }}
              </v-btn>
              <v-btn
                variant="text"
                size="small"
                color="error"
                data-testid="creator-delete-wish-list"
                @click="store.removeWishList(list.id)"
              >
                {{ t('delete.confirm') }}
              </v-btn>
            </div>
          </div>
        </v-card>

        <v-card
          v-if="store.open.kind === 'wish-list' && store.wishList"
          class="mt-4"
          data-testid="creator-wish-list-detail"
        >
          <v-card-title tag="h3">{{ store.wishList.title }}</v-card-title>
          <v-card-text>
            <ul>
              <li v-for="item in store.wishList.items" :key="item.id">
                {{ item.name }} — {{ item.claims.length }} / {{ item.wantedCount }}
              </li>
            </ul>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" data-testid="creator-close-wish-list" @click="store.close()">
              {{ t('creator.surfaceClosed') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </section>
    </template>
  </v-container>
</template>

<style scoped>
/* Identical to the admin poll and wish-list rows, because they are the same kind of thing. */
.rf-row + .rf-row { margin-block-start: var(--rf-stack); }

.rf-row__body {
  display: flex;
  align-items: flex-start;
  gap: 24px;
  padding: var(--rf-card-pad);
}

.rf-row__main { flex: 1 1 auto; min-width: 0; }

.rf-row__title {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}

.rf-row__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  flex-shrink: 0;
}

@media (max-width: 600px) {
  .rf-row__body { flex-direction: column; gap: 16px; }
  .rf-row__actions { width: 100%; justify-content: space-between; }
}
</style>
