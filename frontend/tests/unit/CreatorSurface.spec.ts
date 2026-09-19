import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  fetchCreatorSurface: vi.fn(),
  createPollAsCreator: vi.fn(),
  createWishListAsCreator: vi.fn(),
  deletePollAsCreator: vi.fn(),
  deleteWishListAsCreator: vi.fn(),
  fetchPollResultsAsCreator: vi.fn(),
  fetchWishListAsCreator: vi.fn(),
  creatorExportUrl: (token: string, pollId: string) => `/api/v1/e/${token}/polls/${pollId}/export`,
}))

import { fetchCreatorSurface, fetchPollResultsAsCreator } from '../../src/api/client'
import CreatorSurface from '../../src/components/creator/CreatorSurface.vue'
import de from '../../src/locales/de.json'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

const mountSurface = () =>
  mountComponent(CreatorSurface, { props: { creatorToken: 'abcdefghijklmnopqrstuv' } })

const aPoll = (overrides: Record<string, unknown> = {}) => ({
  id: 'p1',
  title: 'Grillabend',
  participantToken: 'ptok',
  retentionDeadline: '2099-01-01T00:00:00Z',
  responseCount: 4,
  dayCount: 2,
  ...overrides,
})

const aList = (overrides: Record<string, unknown> = {}) => ({
  id: 'w1',
  title: 'Sommerfest',
  targetDate: '2099-07-18',
  closed: false,
  itemCount: 3,
  entryCount: 3,
  placeCount: 6,
  untakenItemCount: 2,
  completeItemCount: 0,
  listToken: 'ltok',
  ...overrides,
})

beforeEach(() => {
  vi.mocked(fetchCreatorSurface).mockReset()
  vi.mocked(fetchPollResultsAsCreator).mockReset()
})

describe('the creator surface', () => {
  it('greets the holder by the name the operator gave the link (FR-007)', async () => {
    vi.mocked(fetchCreatorSurface).mockResolvedValue({ name: 'Anna', polls: [], wishLists: [] })
    const wrapper = mountSurface()
    await flush()

    expect(wrapper.find('[data-testid="creator-greeting"]').text()).toContain('Anna')
  })

  it('states what the link grants before anything can be shared (FR-058)', async () => {
    // The bearer-link design's actual failure mode is somebody forwarding the link believing it
    // is read-only. Saying so costs nothing and is the only mitigation available.
    vi.mocked(fetchCreatorSurface).mockResolvedValue({ name: 'Anna', polls: [], wishLists: [] })
    const wrapper = mountSurface()
    await flush()

    expect(wrapper.find('[data-testid="creator-link-warning"]').text())
      .toBe(de.creator.surfaceWarning)
  })

  it('renders both lists, and says so when each is empty (US1 scenario 4)', async () => {
    vi.mocked(fetchCreatorSurface).mockResolvedValue({ name: 'Anna', polls: [], wishLists: [] })
    const wrapper = mountSurface()
    await flush()

    expect(wrapper.find('[data-testid="creator-polls"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="creator-wish-lists"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="creator-polls-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="creator-wish-lists-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="creator-new-poll"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="creator-new-wish-list"]').exists()).toBe(true)
  })

  it('carries enough summary on each row to find something without opening it (FR-028g)', async () => {
    // This is what pays for there being no deep link to one poll's answers. A row that showed
    // only a title would make the absence of an address a real cost rather than an accepted one.
    vi.mocked(fetchCreatorSurface).mockResolvedValue({
      name: 'Anna',
      polls: [aPoll()],
      wishLists: [aList()],
    })
    const wrapper = mountSurface()
    await flush()

    const pollRow = wrapper.find('[data-testid="creator-poll-row"]')
    expect(pollRow.text()).toContain('Grillabend')
    expect(pollRow.text()).toContain('4')
    expect(pollRow.text()).toContain('2')

    const listRow = wrapper.find('[data-testid="creator-wish-list-row"]')
    expect(listRow.text()).toContain('Sommerfest')
    // Formatted for German, not the bare `DateOnly` string the API returns.
    expect(listRow.text()).toContain('18.07.2099')
  })

  it('opens a poll in place, without a second address (FR-028d, FR-028e)', async () => {
    vi.mocked(fetchCreatorSurface).mockResolvedValue({
      name: 'Anna',
      polls: [aPoll()],
      wishLists: [],
    })
    vi.mocked(fetchPollResultsAsCreator).mockResolvedValue({
      title: 'Grillabend',
      message: null,
      days: [],
      totals: [],
      responses: [{ id: 'r1', displayName: 'Ben', answers: [] }],
      page: 1,
      pageCount: 1,
      responseCount: 1,
    })

    const wrapper = mountSurface()
    await flush()

    expect(wrapper.find('[data-testid="creator-poll-answers"]').exists()).toBe(false)

    await wrapper.find('[data-testid="creator-open-poll"]').trigger('click')
    await flush()

    // Opened within the same page: both lists are still rendered around it.
    expect(wrapper.find('[data-testid="creator-poll-answers"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="creator-polls"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="creator-wish-lists"]').exists()).toBe(true)
  })

  it('offers no upload, no import, no dashboard and no way into the admin area', async () => {
    // FR-028a, FR-028b, FR-029. Absence in the interface is not a security boundary - the server
    // refuses these too, and CreatorSurfaceRefusalTests proves that - but a control that is
    // offered and then refused is a control somebody will file a bug about.
    vi.mocked(fetchCreatorSurface).mockResolvedValue({
      name: 'Anna',
      polls: [aPoll()],
      wishLists: [aList()],
    })
    const wrapper = mountSurface()
    await flush()

    expect(wrapper.find('input[type="file"]').exists()).toBe(false)
    expect(wrapper.html()).not.toContain('/admin')
    expect(wrapper.find('[data-testid="admin-nav"]').exists()).toBe(false)

    for (const absent of ['creator-import', 'creator-dashboard', 'creator-backup']) {
      expect(wrapper.find(`[data-testid="${absent}"]`).exists()).toBe(false)
    }
  })

  it('says one thing when the link cannot be used (FR-008)', async () => {
    // The server answers one neutral payload for unknown, malformed and revoked alike, so the
    // interface cannot say which - and must not pretend to.
    vi.mocked(fetchCreatorSurface).mockRejectedValue({ code: 'not_found' })
    const wrapper = mountSurface()
    await flush()

    expect(wrapper.find('[data-testid="creator-link-unusable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="creator-polls"]').exists()).toBe(false)
  })
})

describe('the store never holds another owner’s content (FR-032)', () => {
  it('renders exactly what the server returned, and nothing merged in', async () => {
    // The surface has no other source of polls or wish lists - no admin store, no shared cache -
    // so nothing can be revealed by a rendering change alone.
    vi.mocked(fetchCreatorSurface).mockResolvedValue({
      name: 'Anna',
      polls: [aPoll({ id: 'mine', title: 'Meiner' })],
      wishLists: [],
    })
    const wrapper = mountSurface()
    await flush()

    const rows = wrapper.findAll('[data-testid="creator-poll-row"]')
    expect(rows).toHaveLength(1)
    expect(rows[0].attributes('data-poll-id')).toBe('mine')
  })
})
