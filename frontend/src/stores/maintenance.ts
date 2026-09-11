import { defineStore } from 'pinia'
import { ref } from 'vue'
import { readMaintenance, setMaintenance, type ApiProblem } from '../api/client'

/**
 * Maintenance state as the admin area sees it.
 *
 * Read from the server rather than remembered locally: the state outlives this browser (FR-029),
 * so a page that trusted its own memory would show "off" after a restart that left it on.
 */
export const useMaintenanceStore = defineStore('maintenance', () => {
  const enabled = ref(false)
  const since = ref<string | null>(null)
  const busy = ref(false)
  const problem = ref<ApiProblem | null>(null)

  async function load() {
    problem.value = null
    try {
      const state = await readMaintenance()
      enabled.value = state.enabled
      since.value = state.since
    } catch (failure) {
      problem.value = failure as ApiProblem
    }
  }

  async function set(next: boolean) {
    if (busy.value) return
    busy.value = true
    problem.value = null
    try {
      const state = await setMaintenance(next)
      enabled.value = state.enabled
      since.value = state.since
    } catch (failure) {
      problem.value = failure as ApiProblem
    } finally {
      busy.value = false
    }
  }

  return { enabled, since, busy, problem, load, set }
})
