import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { fetchDatabaseStatus, fetchMessage } from '../api/client'

/**
 * The UI state machine from data-model.md §3. The backend reports only two states; the third,
 * `backendUnreachable`, is derived here because a dead backend cannot report its own absence
 * (research.md R-4).
 */
export type UiStatusState = 'loading' | 'reachable' | 'unreachable' | 'backendUnreachable'

/** Translation keys, never literals - components and tests deal in keys (FR-029, FR-030). */
const STATE_KEYS: Record<UiStatusState, string> = {
  loading: 'status.loading',
  reachable: 'status.database.reachable',
  unreachable: 'status.database.unreachable',
  backendUnreachable: 'status.backend.unreachable',
}

export const useStatusStore = defineStore('status', () => {
  const message = ref<string | null>(null)
  const databaseState = ref<UiStatusState>('loading')
  const checkedAt = ref<string | null>(null)
  const durationMs = ref<number | null>(null)

  const databaseStateKey = computed(() => STATE_KEYS[databaseState.value])

  async function loadMessage(): Promise<void> {
    try {
      message.value = await fetchMessage()
    } catch {
      message.value = null
    }
  }

  async function loadDatabaseStatus(): Promise<void> {
    try {
      const status = await fetchDatabaseStatus()
      // A 2xx carrying "unreachable" means the database is down but the backend is fine.
      databaseState.value = status.state === 'reachable' ? 'reachable' : 'unreachable'
      checkedAt.value = status.checkedAt
      durationMs.value = status.durationMs
    } catch {
      // Any transport failure or non-2xx: the backend itself could not be reached.
      databaseState.value = 'backendUnreachable'
      checkedAt.value = null
      durationMs.value = null
    }
  }

  async function load(): Promise<void> {
    await Promise.all([loadMessage(), loadDatabaseStatus()])
  }

  return {
    message,
    databaseState,
    databaseStateKey,
    checkedAt,
    durationMs,
    loadMessage,
    loadDatabaseStatus,
    load,
  }
})
