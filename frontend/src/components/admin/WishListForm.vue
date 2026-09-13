<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useWishListsStore } from '../../stores/wishLists'
import { useProblemText } from '../../composables/useProblemText'
import ShareLink from '../poll/ShareLink.vue'

const { t } = useI18n()
const store = useWishListsStore()
const problemText = useProblemText()

interface ItemRow {
  name: string
  /** Empty means "the operator said nothing", which the server reads as one (FR-006). */
  wantedCount: string
}

const title = ref('')
const description = ref('')
const targetDate = ref('')
const items = ref<ItemRow[]>([{ name: '', wantedCount: '' }])
const busy = ref(false)
const createdToken = ref<string | null>(null)

function addItem() {
  items.value = [...items.value, { name: '', wantedCount: '' }]
}

function removeItem(index: number) {
  items.value = items.value.filter((_, i) => i !== index)
}

/**
 * The quantity is sent as written, not defaulted here.
 *
 * FR-006 puts the default on the server, so an empty field must arrive as "nothing said" rather
 * than as a 1 this form invented. Filling it in here would mean two places decide what an
 * unstated quantity means.
 */
async function submit() {
  busy.value = true
  try {
    const created = await store.create(
      title.value,
      description.value.trim() === '' ? null : description.value,
      targetDate.value,
      items.value.map((item) => ({
        name: item.name,
        wantedCount: item.wantedCount.trim() === '' ? null : Number(item.wantedCount),
      })),
    )

    if (created) {
      createdToken.value = created.listToken
      title.value = ''
      description.value = ''
      targetDate.value = ''
      items.value = [{ name: '', wantedCount: '' }]
    }
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <!-- The form itself carries the id, not only the action that reveals it: the unit suite and the
       end-to-end suite both wait for the form to appear, and an id on the button alone cannot tell
       "revealed" from "not revealed" (ui-contract section 3). -->
  <v-card class="rf-section-gap" data-testid="wish-list-form">
    <v-card-item>
      <v-card-title tag="h2" class="text-h6">{{ t('wish.createTitle') }}</v-card-title>
    </v-card-item>

    <v-card-text>
      <!-- One gap between every field, from the shared scale, instead of mb-4 remembered on each
           of them and forgotten on two. -->
      <v-form class="rf-form" @submit.prevent="submit">
        <v-text-field
          v-model="title"
          :label="t('wish.title')"
          maxlength="300"
          counter
          data-testid="wish-form-title"
        />

        <v-textarea
          v-model="description"
          :label="t('wish.description')"
          maxlength="2000"
          rows="3"
          counter
          data-testid="wish-form-description"
        />

        <!-- The field's own floating label, like every other field here. A separate v-label above
             one input made the same form ask for a title with the label inside the box and for a
             date with the label outside it. -->
        <v-text-field
          v-model="targetDate"
          type="date"
          :label="t('wish.targetDate')"
          class="rf-field-date"
          data-testid="wish-form-target-date"
        />

        <!-- "Was gebraucht wird" introduces a group of rows, so it is a heading rather than a
             field label pretending to be one. -->
        <div>
          <h3 class="text-overline text-medium-emphasis rf-heading-gap">{{ t('wish.items') }}</h3>

          <div class="rf-stack">
            <div
              v-for="(item, index) in items"
              :key="index"
              class="rf-field-row"
              data-testid="wish-form-item"
            >
              <v-text-field
                v-model="item.name"
                class="rf-field-row__grow"
                :label="t('wish.itemName')"
                maxlength="200"
                :data-testid="`wish-form-item-name-${index}`"
              />
              <!--
                Left empty on purpose. The placeholder shows the 1 that an unstated quantity means,
                and the server is what applies it (FR-006).
              -->
              <v-text-field
                v-model="item.wantedCount"
                class="rf-field-row__narrow"
                :label="t('wish.wantedCount')"
                type="number"
                min="1"
                max="50"
                placeholder="1"
                :data-testid="`wish-form-item-count-${index}`"
              />
              <v-btn
                icon="mdi-close"
                variant="text"
                size="large"
                :aria-label="`${t('wish.removeItem')}: ${item.name || index + 1}`"
                :data-testid="`wish-form-item-remove-${index}`"
                @click="removeItem(index)"
              />
            </div>
          </div>

          <v-btn
            variant="text"
            class="mt-2"
            prepend-icon="mdi-plus"
            data-testid="wish-form-add-item"
            @click="addItem"
          >
            {{ t('wish.addItem') }}
          </v-btn>
        </div>

        <v-alert v-if="store.problem" type="error" role="alert" data-testid="wish-form-problem">
          {{ problemText(store.problem) }}
        </v-alert>

        <div>
          <v-btn
            type="submit"
            color="primary"
            size="large"
            :loading="busy"
            data-testid="wish-form-submit"
          >
            {{ t('wish.submit') }}
          </v-btn>
        </div>

        <div v-if="createdToken" data-testid="wish-form-created">
          <ShareLink
            :path="`/w/${createdToken}`"
            :label="t('wish.shareLink')"
            link-testid="wish-share-url"
          />
        </div>
      </v-form>
    </v-card-text>
  </v-card>
</template>

<style scoped>
/* One gap between every direct child of the form. */
.rf-form {
  display: grid;
  gap: 20px;
}

.rf-field-date { max-width: 16rem; }
</style>
