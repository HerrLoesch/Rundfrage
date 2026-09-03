import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  signIn: vi.fn(),
  signOut: vi.fn(),
  listPolls: vi.fn(),
  createPoll: vi.fn(),
  deletePoll: vi.fn(),
  deleteResponse: vi.fn(),
  fetchPollResults: vi.fn(),
  backupUrl: '/api/v1/admin/backup',
  exportUrl: (pollId: string) => `/api/v1/admin/polls/${pollId}/export`,
}))

vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }))

import { listPolls } from '../../src/api/client'
import PollList from '../../src/components/admin/PollList.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

const aPoll = (id = 'p1') => ({
  id,
  title: 'Grillabend',
  participantToken: 'tok',
  retentionDeadline: '2026-12-24T00:00:00Z',
  responseCount: 2,
  dayCount: 3,
})

describe('PollList (FR-024a, FR-003, FR-013)', () => {
  beforeEach(() => vi.clearAllMocks())

  it('says storage is unavailable rather than showing an empty list', async () => {
    // FR-024a. Both states show no polls, and they mean opposite things: one says "you have not
    // created any yet", the other says "your data cannot be reached right now". Showing the
    // first when the second is true is the kind of quiet lie this requirement exists to prevent.
    vi.mocked(listPolls).mockRejectedValue({ code: 'storage_unavailable' })

    const wrapper = mountComponent(PollList)
    await flush()

    expect(wrapper.find('[data-testid="storage-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="poll-list-empty"]').exists()).toBe(false)
  })

  it('says the list is empty only when the list really is empty', async () => {
    vi.mocked(listPolls).mockResolvedValue([])

    const wrapper = mountComponent(PollList)
    await flush()

    expect(wrapper.find('[data-testid="poll-list-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="storage-unavailable"]').exists()).toBe(false)
  })

  it('offers a backup download that carries no poll with it', async () => {
    // FR-003: the backup is the whole storage, not one poll, so it belongs beside the list
    // rather than on a card.
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountComponent(PollList)
    await flush()

    const backup = wrapper.get('[data-testid="download-backup"]')
    expect(backup.attributes('href')).toBe('/api/v1/admin/backup')
  })

  it('offers an export per poll, addressed to that poll', async () => {
    // FR-013: one document per poll. A single "export everything" button would be a different
    // requirement, and a link that ignored the poll id would export the wrong one.
    vi.mocked(listPolls).mockResolvedValue([aPoll('abc'), aPoll('def')])

    const wrapper = mountComponent(PollList)
    await flush()

    const exports = wrapper.findAll('[data-testid="export-poll"]')
    expect(exports).toHaveLength(2)
    expect(exports[0].attributes('href')).toBe('/api/v1/admin/polls/abc/export')
    expect(exports[1].attributes('href')).toBe('/api/v1/admin/polls/def/export')
  })
})
