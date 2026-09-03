import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('../../src/api/client', () => ({
  fetchPoll: vi.fn(),
  submitResponse: vi.fn(),
  fetchOwnResponse: vi.fn(),
  reviseResponse: vi.fn(),
}))

import { fetchPoll, submitResponse } from '../../src/api/client'
import { useAnsweringStore } from '../../src/stores/answering'

const POLL = {
  title: 'Grillabend',
  message: null,
  days: [{ id: 'day-1', date: '2026-11-18' }],
  totals: [],
  responses: [],
  page: 1,
  pageCount: 1,
  responseCount: 0,
}

describe('answering store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    vi.mocked(fetchPoll).mockResolvedValue(POLL as never)
  })

  it('holds the edit token after a successful submission', async () => {
    // That token is what turns every later save into a revision rather than a new response.
    vi.mocked(submitResponse).mockResolvedValue({ responseId: 'r1', editToken: 'tok-1' })

    const store = useAnsweringStore()
    store.displayName = 'Anna'
    store.setAnswer('day-1', 'yes')
    await store.submit('poll-token')

    expect(store.editToken).toBe('tok-1')
    expect(store.justSubmitted).toBe(true)
  })

  it('forgets one poll before showing another', async () => {
    // The store is a singleton. Without this, opening a second poll link in the same session
    // would carry the previous poll's name, answers and edit token into it - and a save would
    // then revise a response belonging to a different poll.
    vi.mocked(submitResponse).mockResolvedValue({ responseId: 'r1', editToken: 'tok-1' })

    const store = useAnsweringStore()
    store.displayName = 'Anna'
    store.setAnswer('day-1', 'yes')
    await store.submit('poll-a')

    await store.loadPoll('poll-b')

    expect(store.editToken).toBeNull()
    expect(store.displayName).toBe('')
    expect(store.answers).toEqual({})
    expect(store.justSubmitted).toBe(false)
  })
})
