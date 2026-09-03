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
  // Without this the form simply sat there after a correct password: the cookie was set, the
  // API accepted it, and the operator never left the sign-in page.
  if (await session.signIn(user.value, password.value)) {
    await router.push({ name: 'admin-polls' })
  }
}
</script>

<template>
  <form class="sign-in" data-testid="sign-in-form" @submit.prevent="submit">
    <h1>{{ t('signIn.title') }}</h1>

    <label for="sign-in-user">{{ t('signIn.user') }}</label>
    <input id="sign-in-user" v-model="user" data-testid="sign-in-user" type="text" autocomplete="username" />

    <label for="sign-in-password">{{ t('signIn.password') }}</label>
    <input
      id="sign-in-password"
      v-model="password"
      data-testid="sign-in-password"
      type="password"
      autocomplete="current-password"
    />

    <button type="submit" data-testid="sign-in-submit">{{ t('signIn.submit') }}</button>

    <p v-if="errorText" class="error" role="alert" data-testid="sign-in-error">{{ errorText }}</p>
  </form>
</template>

<style scoped>
.sign-in { display: flex; flex-direction: column; gap: 0.5rem; max-width: 22rem; padding: 2rem; font-family: system-ui, sans-serif; }
label { font-weight: 600; }
input { padding: 0.5rem; font: inherit; }
button { padding: 0.6rem; font: inherit; cursor: pointer; margin-top: 0.5rem; }
.error { background: #fdecea; color: #b71c1c; border-left: 4px solid #c62828; padding: 0.5rem 0.75rem; }
</style>
