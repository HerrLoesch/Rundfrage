import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('../../src/api/client', () => ({ fetchDashboard: vi.fn() }))

import { fetchDashboard } from '../../src/api/client'
import { useDashboardStore } from '../../src/stores/dashboard'

const figures = {
  pollCount: 2,
  responseCount: 7,
  unansweredPolls: 0,
  deletionsDueSoon: 1,
  nextDeletion: '2026-09-20T00:00:00Z',
  yes: 5,
  maybe: 1,
  no: 1,
}

describe('dashboard store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('holds no figures until a read succeeds', () => {
    const store = useDashboardStore()

    expect(store.figures).toBeNull()
    expect(store.loadProblem).toBeNull()
  })

  it('keeps the figures a successful read returned', async () => {
    vi.mocked(fetchDashboard).mockResolvedValue(figures as never)

    const store = useDashboardStore()
    await store.load()

    expect(store.figures).toEqual(figures)
    expect(store.loading).toBe(false)
    expect(store.loadProblem).toBeNull()
  })

  it('records the problem and holds no figures when the read fails', async () => {
    vi.mocked(fetchDashboard).mockRejectedValue({ code: 'storage_unavailable' })

    const store = useDashboardStore()
    await store.load()

    expect(store.loadProblem).toEqual({ code: 'storage_unavailable' })
    expect(store.figures).toBeNull()
  })

  it('discards figures from an earlier successful read when a later one fails', async () => {
    // Otherwise the operator would be left reading numbers the system has just said it cannot
    // produce (FR-032).
    vi.mocked(fetchDashboard)
      .mockResolvedValueOnce(figures as never)
      .mockRejectedValueOnce({ code: 'storage_unavailable' })

    const store = useDashboardStore()
    await store.load()
    expect(store.figures).not.toBeNull()

    await store.load()
    expect(store.figures).toBeNull()
  })

  it('distinguishes a refused session from an unreachable store', async () => {
    vi.mocked(fetchDashboard).mockRejectedValue({ code: 'unauthorized' })

    const store = useDashboardStore()
    await store.load()

    // The view redirects on this one and shows storage wording on the other, so the code has to
    // survive the round trip into the store.
    expect(store.loadProblem?.code).toBe('unauthorized')
  })
})
