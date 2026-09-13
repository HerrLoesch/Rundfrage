import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchDashboard, type ApiProblem, type DashboardView } from '../api/client'

/**
 * The dashboard's figures as the admin area sees them.
 *
 * The same three fields the polls store uses - value, loading, loadProblem - so the unauthorized
 * redirect and the useProblemText composable carry over unchanged rather than being reinvented.
 *
 * `figures` stays null until a read succeeds, and that matters more here than in a list: a view
 * holding six numbers has to be able to render *no* numbers. Zero is a claim about the data, and
 * FR-031 and FR-032 both forbid making it when the truth is "nothing stored" or "cannot be read".
 */
export const useDashboardStore = defineStore('dashboard', () => {
  const figures = ref<DashboardView | null>(null)
  const loading = ref(false)
  const loadProblem = ref<ApiProblem | null>(null)

  async function load(): Promise<void> {
    loading.value = true
    loadProblem.value = null
    try {
      figures.value = await fetchDashboard()
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
      // Cleared on purpose. Keeping the previous read would leave the operator looking at figures
      // the system has just said it cannot produce (FR-032).
      figures.value = null
    } finally {
      loading.value = false
    }
  }

  return { figures, loading, loadProblem, load }
})
