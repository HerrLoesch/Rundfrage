<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { useWishListsStore, filledPercent, isComplete } from '../../stores/wishLists'
import { useSessionStore } from '../../stores/session'
import { useProblemText } from '../../composables/useProblemText'
import ShareLink from '../poll/ShareLink.vue'
import PageHeader from '../layout/PageHeader.vue'
import type { WishClaimView, WishItemDetail } from '../../api/client'

const props = defineProps<{ wishListId: string }>()

const { t } = useI18n()
const router = useRouter()
const store = useWishListsStore()
const session = useSessionStore()
const problemText = useProblemText()

const title = ref('')
const description = ref('')
const targetDate = ref('')
const newItemName = ref('')
const newItemCount = ref('')
const removingItem = ref<WishItemDetail | null>(null)
const removingClaim = ref<{ claim: WishClaimView; item: WishItemDetail } | null>(null)

/**
 * Its own address, so this page can be linked to and survives a reload (FR-043, SC-008): it loads
 * from the route parameter rather than from anything the list page left behind.
 */
onMounted(async () => {
  await store.open(props.wishListId)

  if (store.loadProblem?.code === 'unauthorized') {
    session.isSignedIn = false
    await router.push({ name: 'sign-in' })
    return
  }

  // The list is gone - deleted in another tab, or never there. Say so on the page that can show
  // what does exist, rather than presenting an empty detail.
  if (store.loadProblem?.code === 'not_found') {
    await router.push({ name: 'wish-lists', query: { gone: '1' } })
  }
})

watch(
  () => store.current,
  (list) => {
    if (!list) return
    title.value = list.title
    description.value = list.description ?? ''
    targetDate.value = list.targetDate
  },
  { immediate: true },
)

const storageUnavailable = computed(
  () =>
    store.loadProblem !== null &&
    store.loadProblem.code !== 'unauthorized' &&
    store.loadProblem.code !== 'not_found',
)

function saveList() {
  return store.edit({
    title: title.value,
    description: description.value.trim() === '' ? null : description.value,
    targetDate: targetDate.value,
  })
}

async function addItem() {
  const added = await store.addItem({
    name: newItemName.value,
    wantedCount: newItemCount.value.trim() === '' ? null : Number(newItemCount.value),
  })

  if (added) {
    newItemName.value = ''
    newItemCount.value = ''
  }
}

/**
 * Both item fields are bound one-way to the store and written back on change, so a refusal leaves
 * the field showing what the server rejected while the store still holds the truth. Vue will not
 * re-render it either: the bound value never changed. `restore` puts the field back to the stored
 * value, so the message and the field agree about what the item is called and how many are wanted.
 */
function restore(event: Event, value: string | number) {
  const input = event.target as HTMLInputElement
  input.value = String(value)
}

async function rename(item: WishItemDetail, event: Event) {
  const name = (event.target as HTMLInputElement).value

  if (name.trim() === '' || name === item.name) {
    restore(event, item.name)
    return
  }

  if (!(await store.editItem(item.id, { name }))) {
    restore(event, item.name)
  }
}

async function setCount(item: WishItemDetail, event: Event) {
  const wantedCount = Number((event.target as HTMLInputElement).value)

  if (!Number.isFinite(wantedCount) || wantedCount === item.wantedCount) {
    restore(event, item.wantedCount)
    return
  }

  if (!(await store.editItem(item.id, { wantedCount }))) {
    restore(event, item.wantedCount)
  }
}

async function confirmRemoveItem() {
  const item = removingItem.value
  if (!item) return
  await store.removeItem(item.id)
  removingItem.value = null
}

async function confirmRemoveClaim() {
  const pending = removingClaim.value
  if (!pending) return
  await store.removeClaim(pending.claim.id)
  removingClaim.value = null
}
</script>

<template>
  <v-container class="rf-page rf-page--admin">
    <!-- Pulled tight to the title it belongs to, and inset by the button's own padding so its
         text lines up with the heading below rather than sitting 16px to its left. -->
    <v-btn
      variant="text"
      size="small"
      prepend-icon="mdi-arrow-left"
      class="rf-back"
      data-testid="wish-back"
      :to="{ name: 'wish-lists' }"
    >
      {{ t('wish.backToList') }}
    </v-btn>

    <v-alert v-if="storageUnavailable" type="warning" class="mb-4" data-testid="wish-detail-unavailable">
      {{ t('storage.unavailable') }}
    </v-alert>

    <div v-else-if="store.loading" class="text-center py-8" data-testid="wish-detail-loading">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else-if="store.current">
      <PageHeader :title="store.current.title" title-testid="wish-detail-title">
        <template #badge>
          <v-chip v-if="store.current.closed" data-testid="wish-detail-closed">
            {{ t('wish.closed') }}
          </v-chip>
        </template>

        <template #meta>

          <p class="rf-meta text-medium-emphasis mt-2" data-testid="wish-detail-figures">
            <span class="rf-meta__item">
              <v-icon icon="mdi-account-multiple-outline" size="16" />
              {{ t('wish.filled', {
                filled: store.current.entryCount,
                places: store.current.placeCount,
                percent: filledPercent(store.current.entryCount, store.current.placeCount),
              }) }}
            </span>
            <span
              v-if="isComplete(store.current.entryCount, store.current.placeCount)"
              class="rf-meta__item font-weight-medium"
              data-testid="wish-detail-complete"
            >
              {{ t('wish.complete') }}
            </span>
          </p>
        </template>
      </PageHeader>

      <!-- Closed is the clock, not a control: the way back is a later target date (FR-028c). -->
      <v-alert
        v-if="store.current.closed"
        type="info"
        class="rf-section-gap"
        data-testid="wish-detail-closed-hint"
      >
        {{ t('wish.closedHint') }}
      </v-alert>

      <ShareLink
        :path="`/w/${store.current.listToken}`"
        :label="t('wish.shareLink')"
        link-testid="wish-detail-share"
        class="rf-section-gap"
      />

      <v-alert v-if="store.problem" type="error" class="mb-4" role="alert" data-testid="wish-detail-problem">
        {{ problemText(store.problem) }}
      </v-alert>

      <v-card class="rf-section-gap" data-testid="wish-detail-edit">
        <v-card-item>
          <v-card-title tag="h2" class="text-h6">{{ t('wish.editList') }}</v-card-title>
        </v-card-item>

        <v-card-text>
          <v-form class="rf-form" @submit.prevent="saveList">
            <v-text-field
              v-model="title"
              :label="t('wish.title')"
              maxlength="300"
              counter
              data-testid="wish-edit-title"
            />
            <v-textarea
              v-model="description"
              :label="t('wish.description')"
              rows="3"
              maxlength="2000"
              counter
              data-testid="wish-edit-description"
            />
            <!--
              The field's own floating label, like the two above it. A separate v-label used to
              sit over this one input, so the same form asked for a title with the label inside
              the box and for a date with the label outside it - two mechanisms, one form.
            -->
            <v-text-field
              v-model="targetDate"
              type="date"
              :label="t('wish.targetDate')"
              class="rf-field-date"
              data-testid="wish-edit-target-date"
            />

            <div>
              <v-btn type="submit" color="primary" data-testid="wish-edit-save">
                {{ t('wish.save') }}
              </v-btn>
            </div>
          </v-form>
        </v-card-text>
      </v-card>

      <h2 class="rf-title text-h5 rf-heading-gap">{{ t('wish.items') }}</h2>

      <v-card
        v-for="item in store.current.items"
        :key="item.id"
        class="rf-item"
        data-testid="wish-detail-item"
        :data-item-id="item.id"
      >
        <!--
          One padded block again, and `rf-field-row--inset` for the reason that class exists: an
          outlined field hangs its floating label above the input's top edge, and directly inside
          a v-card-item that label was clipped by the card's border - "Bezeichnung" rendered as a
          half-height smear along the top of every item.
        -->
        <div class="rf-item__body">
          <div class="rf-field-row rf-field-row--inset">
            <v-text-field
              class="rf-field-row__grow"
              :model-value="item.name"
              :label="t('wish.itemName')"
              maxlength="200"
              :data-testid="`wish-item-name-${item.id}`"
              @change="rename(item, $event)"
            />
            <v-text-field
              class="rf-field-row__narrow"
              :model-value="item.wantedCount"
              :label="t('wish.wantedCount')"
              type="number"
              min="1"
              max="50"
              :data-testid="`wish-item-count-${item.id}`"
              @change="setCount(item, $event)"
            />
            <v-btn
              variant="text"
              color="error"
              size="large"
              prepend-icon="mdi-delete-outline"
              :data-testid="`wish-item-remove-${item.id}`"
              @click="removingItem = item"
            >
              {{ t('wish.deleteItem') }}
            </v-btn>
          </div>

          <p v-if="item.claims.length === 0" class="text-body-2 text-medium-emphasis mt-3 mb-0">
            {{ t('wish.noNames') }}
          </p>

          <div v-else class="d-flex flex-wrap ga-2 mt-3">
            <v-chip
              v-for="claim in item.claims"
              :key="claim.id"
              closable
              :data-testid="`wish-claim-${claim.id}`"
              :close-label="t('wish.deleteClaim')"
              @click:close="removingClaim = { claim, item }"
            >
              {{ claim.displayName }}
            </v-chip>
          </div>
        </div>
      </v-card>

      <v-card class="rf-section" data-testid="wish-add-item">
        <v-card-item>
          <v-card-title tag="h3" class="text-h6">{{ t('wish.addItem') }}</v-card-title>
        </v-card-item>

        <v-card-text>
          <!-- The same row shape the items above use, so adding one looks like editing one. -->
          <v-form @submit.prevent="addItem">
            <div class="rf-field-row">
              <v-text-field
                v-model="newItemName"
                class="rf-field-row__grow"
                :label="t('wish.itemName')"
                maxlength="200"
                data-testid="wish-add-item-name"
              />
              <v-text-field
                v-model="newItemCount"
                class="rf-field-row__narrow"
                :label="t('wish.wantedCount')"
                type="number"
                min="1"
                max="50"
                placeholder="1"
                data-testid="wish-add-item-count"
              />
              <v-btn
                type="submit"
                color="primary"
                size="large"
                data-testid="wish-add-item-submit"
              >
                {{ t('wish.addItem') }}
              </v-btn>
            </div>
          </v-form>
        </v-card-text>
      </v-card>

      <!-- FR-034: the confirmation says how many entries die with the item, before it happens. -->
      <v-dialog :model-value="removingItem !== null" persistent
                @update:model-value="removingItem = null">
        <v-card v-if="removingItem" data-testid="wish-item-remove-confirm" role="alertdialog">
          <v-card-title>{{ t('delete.confirmTitle') }}</v-card-title>
          <v-card-text>
            {{ t('wish.deleteItemConfirm', {
              name: removingItem.name,
              count: removingItem.claims.length,
            }) }}
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="removingItem = null">{{ t('delete.cancel') }}</v-btn>
            <v-btn color="error" data-testid="wish-item-remove-confirmed" @click="confirmRemoveItem">
              {{ t('delete.confirm') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-dialog :model-value="removingClaim !== null" persistent
                @update:model-value="removingClaim = null">
        <v-card v-if="removingClaim" data-testid="wish-claim-remove-confirm" role="alertdialog">
          <v-card-title>{{ t('delete.confirmTitle') }}</v-card-title>
          <v-card-text>
            {{ t('wish.deleteClaimConfirm', {
              name: removingClaim.claim.displayName,
              item: removingClaim.item.name,
            }) }}
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="removingClaim = null">{{ t('delete.cancel') }}</v-btn>
            <v-btn color="error" data-testid="wish-claim-remove-confirmed" @click="confirmRemoveClaim">
              {{ t('delete.confirm') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
    </template>
  </v-container>
</template>

<style scoped>
/* The back link sits above the title and is inset by its own button padding, so its text lines
   up with the heading rather than hanging to the left of it. */
.rf-back {
  margin-inline-start: -8px;
  margin-block-end: 12px;
}


/* One gap between every field of a form, rather than mb-4 remembered on each of them. */
.rf-form {
  display: grid;
  gap: 20px;
}

/* A date input has a fixed content width; letting it span the card looks like a mistake. */
.rf-field-date { max-width: 16rem; }

.rf-item + .rf-item { margin-block-start: var(--rf-stack); }

.rf-item__body { padding: var(--rf-card-pad); }
</style>
