import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('../../src/api/client', () => ({
  fetchMessage: vi.fn(),
  fetchDatabaseStatus: vi.fn(),
}))

import { fetchMessage, fetchDatabaseStatus } from '../../src/api/client'
import { useStatusStore } from '../../src/stores/status'

describe('status store - tri-state derivation (data-model.md §3)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(fetchMessage).mockResolvedValue('irrelevant')
  })

  it('maps a 2xx response carrying "reachable" to the reachable state', async () => {
    vi.mocked(fetchDatabaseStatus).mockResolvedValue({
      state: 'reachable',
      checkedAt: '2026-09-02T09:00:00.000Z',
      durationMs: 12,
    })

    const store = useStatusStore()
    await store.loadDatabaseStatus()

    expect(store.databaseState).toBe('reachable')
  })

  it('maps a 2xx response carrying "unreachable" to the unreachable state', async () => {
    // The endpoint answers 200 even when the database is down; the state lives in the body.
    vi.mocked(fetchDatabaseStatus).mockResolvedValue({
      state: 'unreachable',
      checkedAt: '2026-09-02T09:00:00.000Z',
      durationMs: 2000,
    })

    const store = useStatusStore()
    await store.loadDatabaseStatus()

    expect(store.databaseState).toBe('unreachable')
  })

  it('maps a failed request to backendUnreachable, not to unreachable', async () => {
    // research.md R-4: a dead backend cannot report itself, so the client derives this state.
    vi.mocked(fetchDatabaseStatus).mockRejectedValue(new Error('network down'))

    const store = useStatusStore()
    await store.loadDatabaseStatus()

    expect(store.databaseState).toBe('backendUnreachable')
  })

  it('starts in the loading state before anything has been fetched', async () => {
    const store = useStatusStore()
    expect(store.databaseState).toBe('loading')
  })

  it('recovers to reachable on a later load without any restart', async () => {
    // SC-005
    const store = useStatusStore()

    vi.mocked(fetchDatabaseStatus).mockRejectedValueOnce(new Error('down'))
    await store.loadDatabaseStatus()
    expect(store.databaseState).toBe('backendUnreachable')

    vi.mocked(fetchDatabaseStatus).mockResolvedValue({
      state: 'reachable',
      checkedAt: '2026-09-02T09:01:00.000Z',
      durationMs: 8,
    })
    await store.loadDatabaseStatus()
    expect(store.databaseState).toBe('reachable')
  })

  it('exposes the translation key for the current state, never a literal', async () => {
    // FR-029 / FR-030: components and tests deal in keys, not German text.
    vi.mocked(fetchDatabaseStatus).mockResolvedValue({
      state: 'unreachable',
      checkedAt: '2026-09-02T09:00:00.000Z',
      durationMs: 2000,
    })

    const store = useStatusStore()
    await store.loadDatabaseStatus()

    expect(store.databaseStateKey).toBe('status.database.unreachable')
  })
})
