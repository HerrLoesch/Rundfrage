import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent, de } from '../support/mount'

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

import { createRouter, createMemoryHistory } from 'vue-router'
import { createPoll, listPolls } from '../../src/api/client'
import { usePollsStore } from '../../src/stores/polls'
import { routes } from '../../src/router'
import PollList from '../../src/components/admin/PollList.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

/**
 * A real router rather than a mocked module.
 *
 * The control that opens a poll's answers is now a link, and a link's truth is the address it
 * carries (007 FR-014a). With vue-router mocked, Vuetify renders a button with no href and the
 * test would pass against something a person cannot follow.
 */
const router = createRouter({ history: createMemoryHistory(), routes })

const mountList = () => mountComponent(PollList, { global: { plugins: [router] } })

/** Vuetify puts the fallthrough attribute on the field wrapper, not the input inside it. */
const inputIn = (wrapper: ReturnType<typeof mountList>, testId: string) =>
  wrapper.get(`[data-testid="${testId}"]`).get('input')

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

    const wrapper = mountList()
    await flush()

    expect(wrapper.find('[data-testid="storage-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="poll-list-empty"]').exists()).toBe(false)
  })

  it('says the list is empty only when the list really is empty', async () => {
    vi.mocked(listPolls).mockResolvedValue([])

    const wrapper = mountList()
    await flush()

    expect(wrapper.find('[data-testid="poll-list-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="storage-unavailable"]').exists()).toBe(false)
  })

  it('does not call a rejected form entry a storage failure', async () => {
    // Both used to read one field on the store. A poll created with an empty title therefore
    // produced the validation message *and* "your data cannot be reached" above it - two
    // different accounts of the same event, one of them false and alarming.
    vi.mocked(listPolls).mockResolvedValue([])
    vi.mocked(createPoll).mockRejectedValue({ code: 'title_required' })

    const wrapper = mountList()
    await flush()

    await usePollsStore().create('', null, ['2026-11-20'])
    await flush()

    expect(wrapper.find('[data-testid="storage-unavailable"]').exists()).toBe(false)
  })

  it('shows the poll address as a link that can be followed', async () => {
    // 004 FR-015, FR-016, FR-016b. It looked like a link and behaved like a paragraph.
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountList()
    await flush()

    const link = wrapper.get('[data-testid="poll-list-link"]')
    expect(link.element.tagName).toBe('A')
    expect(link.attributes('href')).toContain('/u/tok')
    expect(link.attributes('target')).toBe('_blank')

    // The opened page gets no handle on the tab that opened it.
    const rel = link.attributes('rel') ?? ''
    expect(rel).toContain('noopener')
    expect(rel).toContain('noreferrer')
  })

  it('keeps the address itself as plain text beside the hidden new-tab note', async () => {
    // 004 FR-016a and FR-017. These addresses are pasted into chat windows far more often than
    // they are clicked, so the visible text must stay the bare address - while a screen reader
    // still hears that a new tab is about to open.
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountList()
    await flush()

    const link = wrapper.get('[data-testid="poll-list-link"]')

    // The link's own text is the address and nothing else. The note is a *description* beside
    // it, not part of it - inside the link it would become part of the address every test and
    // every person copies out of here.
    expect(link.text()).toBe(link.attributes('href'))
    expect(link.find('.d-sr-only').exists()).toBe(false)

    const note = wrapper.get(`#${link.attributes('aria-describedby')}`)
    expect(note.text()).toBe(de.share.newTab)
    expect(note.classes()).toContain('d-sr-only')
  })

  /**
   * 007 FR-015 and SC-002. The backup download, the maintenance switch and the restore used to
   * stand at the top of this page; they are now in settings, and their absence here is the
   * requirement. Asserted negatively on purpose - this is what a careless refactor puts back.
   */
  it('holds no control that maintains the installation (007 FR-015, SC-002)', async () => {
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountList()
    await flush()

    expect(wrapper.find('[data-testid="download-backup"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="maintenance-toggle"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="restore-panel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="sign-out"]').exists()).toBe(false)
  })

  it('offers an export per poll, addressed to that poll', async () => {
    // FR-013: one document per poll. A single "export everything" button would be a different
    // requirement, and a link that ignored the poll id would export the wrong one.
    vi.mocked(listPolls).mockResolvedValue([aPoll('abc'), aPoll('def')])

    const wrapper = mountList()
    await flush()

    const exports = wrapper.findAll('[data-testid="export-poll"]')
    expect(exports).toHaveLength(2)
    expect(exports[0].attributes('href')).toBe('/api/v1/admin/polls/abc/export')
    expect(exports[1].attributes('href')).toBe('/api/v1/admin/polls/def/export')
  })

  // --- 007 US1: the area opens on the list, and the forms are revealed ------------------------

  it('opens on the list, with neither form expanded (FR-014h)', async () => {
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountList()
    await flush()

    expect(wrapper.find('[data-testid="poll-create-toggle"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="poll-import-toggle"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="poll-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="import-panel"]').exists()).toBe(false)
  })

  it('reveals the creation form when asked, and not before (FR-014h)', async () => {
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountList()
    await flush()
    await wrapper.get('[data-testid="poll-create-toggle"]').trigger('click')

    expect(wrapper.find('[data-testid="poll-form"]').exists()).toBe(true)
  })

  it('keeps at most one form open at a time (FR-014i)', async () => {
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountList()
    await flush()

    await wrapper.get('[data-testid="poll-create-toggle"]').trigger('click')
    await wrapper.get('[data-testid="poll-import-toggle"]').trigger('click')

    // Adding one poll and reading a file are different enough intentions that showing both at
    // once would put the page back where this feature found it.
    expect(wrapper.find('[data-testid="poll-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="import-panel"]').exists()).toBe(true)
  })

  it('discards what was entered when a form is closed (FR-014j)', async () => {
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountList()
    await flush()

    await wrapper.get('[data-testid="poll-create-toggle"]').trigger('click')
    await inputIn(wrapper, 'poll-title').setValue('Halb geschrieben')
    await wrapper.get('[data-testid="poll-create-toggle"]').trigger('click')
    await wrapper.get('[data-testid="poll-create-toggle"]').trigger('click')

    // Unmounted rather than hidden: a poll half-written yesterday must not be one click from
    // being created today.
    expect((inputIn(wrapper, 'poll-title').element as HTMLInputElement).value).toBe('')
  })

  it('shows summaries only, never a poll\'s answers inline (FR-014b)', async () => {
    vi.mocked(listPolls).mockResolvedValue([aPoll()])

    const wrapper = mountList()
    await flush()

    expect(wrapper.find('[data-testid="poll-list-item"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="results-grid"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="summary-row"]').exists()).toBe(false)
  })

  it('leads to a poll\'s answers by address rather than by expanding them (FR-014a)', async () => {
    vi.mocked(listPolls).mockResolvedValue([aPoll('p9')])

    const wrapper = mountList()
    await flush()

    const open = wrapper.get('[data-testid="show-results"]')
    expect(open.attributes('href')).toBe('/admin/terminfindungen/p9')
  })

  it('offers creating a poll from the empty state, without finding the action first (FR-014l)', async () => {
    vi.mocked(listPolls).mockResolvedValue([])

    const wrapper = mountList()
    await flush()

    const empty = wrapper.get('[data-testid="poll-list-empty"]')
    expect(empty.text()).toContain(de.poll.empty)

    await wrapper.get('[data-testid="poll-create-empty"]').trigger('click')
    expect(wrapper.find('[data-testid="poll-form"]').exists()).toBe(true)
  })
})
