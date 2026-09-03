import { defineStore } from 'pinia'
import { ref } from 'vue'
import { signIn as apiSignIn, signOut as apiSignOut, type ApiProblem } from '../api/client'

/**
 * The operator session. Deliberately holds no token: the session lives in an HttpOnly cookie
 * the browser attaches on its own, so a script - including a malicious one - cannot read it
 * (research.md R-1). This flag only drives what the interface shows.
 */
export const useSessionStore = defineStore('session', () => {
  const isSignedIn = ref(false)
  const problem = ref<ApiProblem | null>(null)

  async function signIn(user: string, password: string): Promise<boolean> {
    problem.value = null
    try {
      await apiSignIn(user, password)
      isSignedIn.value = true
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      isSignedIn.value = false
      return false
    }
  }

  async function signOut(): Promise<void> {
    try {
      await apiSignOut()
    } finally {
      // Whatever the server said, the local view of the session is over (FR-007).
      isSignedIn.value = false
    }
  }

  return { isSignedIn, problem, signIn, signOut }
})
