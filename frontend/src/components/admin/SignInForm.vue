<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useSessionStore } from '../../stores/session'
import { useProblemText } from '../../composables/useProblemText'

const { t } = useI18n()
const router = useRouter()
const session = useSessionStore()
const problemText = useProblemText()

const user = ref('')
const password = ref('')
const busy = ref(false)

const errorText = computed(() => {
  const problem = session.problem
  if (!problem) return ''
  // FR-004 and FR-005: one neutral message, or the lockout with its delay. Neither names a field.
  if (problem.code === 'account_locked') {
    return t('signIn.locked', { minutes: Math.ceil((problem.retryAfterSeconds ?? 0) / 60) })
  }
  return problem.code === 'unauthorized' ? t('signIn.failed') : problemText(problem)
})

async function submit() {
  busy.value = true
  try {
    if (await session.signIn(user.value, password.value)) {
      await router.push({ name: 'admin-polls' })
    }
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <v-container class="fill-height" max-width="480">
    <v-row justify="center" class="w-100">
      <v-col cols="12">
        <v-card class="pa-2">
          <v-card-item>
            <v-card-title tag="h1">{{ t('signIn.title') }}</v-card-title>
          </v-card-item>

          <v-card-text>
            <v-form data-testid="sign-in-form" @submit.prevent="submit">
              <v-text-field
                v-model="user"
                :label="t('signIn.user')"
                data-testid="sign-in-user"
                autocomplete="username"
                prepend-inner-icon="mdi-account-outline"
                class="mb-4"
              />

              <v-text-field
                v-model="password"
                :label="t('signIn.password')"
                data-testid="sign-in-password"
                type="password"
                autocomplete="current-password"
                prepend-inner-icon="mdi-lock-outline"
              />

              <v-alert
                v-if="errorText"
                type="error"
                class="mt-4"
                role="alert"
                data-testid="sign-in-error"
              >
                {{ errorText }}
              </v-alert>

              <v-btn
                type="submit"
                color="primary"
                block
                size="large"
                class="mt-6"
                :loading="busy"
                data-testid="sign-in-submit"
              >
                {{ t('signIn.submit') }}
              </v-btn>
            </v-form>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>
