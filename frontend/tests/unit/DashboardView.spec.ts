import { describe, it, expect, vi, beforeEach } from 'vitest'
import { nextTick } from 'vue'
import { createRouter, createMemoryHistory } from 'vue-router'
import { mountComponent, de } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  fetchDashboard: vi.fn(),
  readMaintenance: vi.fn(async () => ({ enabled: false, since: null })),
  setMaintenance: vi.fn(),
  listPolls: vi.fn(async () => []),
  createPoll: vi.fn(),
  deletePoll: vi.fn(),
  deleteResponse: vi.fn(),
  fetchPollResults: vi.fn(),
  importPoll: vi.fn(),
  previewRestore: vi.fn(),
  restoreBackup: vi.fn(),
  signIn: vi.fn(),
  signOut: vi.fn(),
  exportUrl: (id: string) => `/api/v1/admin/polls/${id}/export`,
  backupUrl: '/api/v1/admin/backup',
  // Feature 008: the dashboard's second read (research R-5).
  listWishLists: vi.fn(async () => []),
  fetchWishList: vi.fn(),
  createWishList: vi.fn(),
  updateWishList: vi.fn(),
  addWishItem: vi.fn(),
  updateWishItem: vi.fn(),
  removeWishItem: vi.fn(),
  deleteWishClaim: vi.fn(),
  deleteWishList: vi.fn(),
}))

import { fetchDashboard, listWishLists } from '../../src/api/client'
import { routes } from '../../src/router'
import { useMaintenanceStore } from '../../src/stores/maintenance'
import DashboardView from '../../src/components/admin/DashboardView.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

const figures = (over: Record<string, unknown> = {}) => ({
  pollCount: 4,
  responseCount: 11,
  unansweredPolls: 1,
  deletionsDueSoon: 2,
  nextDeletion: '2026-09-20T00:00:00Z',
  yes: 20,
  maybe: 5,
  no: 3,
  ...over,
})

async function dashboard(prepare?: () => void) {
  const router = createRouter({ history: createMemoryHistory(), routes })
  await router.push('/admin')
  await router.isReady()

  const push = vi.spyOn(router, 'push').mockResolvedValue(undefined)
  const wrapper = mountComponent(DashboardView, { global: { plugins: [router] } }, prepare)
  await flush()

  return { wrapper, push }
}

/**
 * 007 FR-028 to FR-034 and ui-contract §3.
 *
 * The recurring theme: a number on this page is a claim about the data, so every state in which
 * the system does not know a number must render no number at all.
 */
describe('Dashboard', () => {
  beforeEach(() => vi.clearAllMocks())

  it('presents the six figures, each labelled (FR-028)', async () => {
    vi.mocked(fetchDashboard).mockResolvedValue(figures() as never)

    const { wrapper } = await dashboard()

    for (const [testid, label] of [
      ['stat-polls', de.dashboard.polls],
      ['stat-responses', de.dashboard.responses],
      ['stat-unanswered', de.dashboard.unanswered],
      ['stat-deletions', de.dashboard.deletions],
      ['stat-distribution', de.dashboard.distribution],
      ['stat-maintenance', de.dashboard.maintenance],
    ] as const) {
      const tile = wrapper.get(`[data-testid="${testid}"]`)
      expect(tile.text(), testid).toContain(label)
    }
  })

  it('reports the numbers it was given (FR-029)', async () => {
    vi.mocked(fetchDashboard).mockResolvedValue(figures() as never)

    const { wrapper } = await dashboard()

    expect(wrapper.get('[data-testid="stat-polls"]').text()).toContain('4')
    expect(wrapper.get('[data-testid="stat-responses"]').text()).toContain('11')
    expect(wrapper.get('[data-testid="stat-unanswered"]').text()).toContain('1')
    expect(wrapper.get('[data-testid="distribution-yes"]').text()).toBe('20')
    expect(wrapper.get('[data-testid="distribution-maybe"]').text()).toBe('5')
    expect(wrapper.get('[data-testid="distribution-no"]').text()).toBe('3')
  })

  /**
   * The state this project has been careful about since 003, made sharper: a *list* showing
   * nothing is ambiguous, but a *figure* showing 0 is a positive claim (FR-032).
   */
  it('renders no figure at all when the data cannot be read (FR-032)', async () => {
    vi.mocked(fetchDashboard).mockRejectedValue({ code: 'storage_unavailable' })

    const { wrapper } = await dashboard()

    expect(wrapper.find('[data-testid="storage-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stat-polls"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="stat-distribution"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('0')
  })

  it('says so in words when nothing is stored, rather than printing zeros (FR-031)', async () => {
    vi.mocked(fetchDashboard).mockResolvedValue(figures({ pollCount: 0 }) as never)

    const { wrapper } = await dashboard()

    expect(wrapper.get('[data-testid="dashboard-empty"]').text()).toBe(de.dashboard.empty)
    expect(wrapper.find('[data-testid="stat-polls"]').exists()).toBe(false)
  })

  it('renders no number while it is still loading', async () => {
    // Two seconds are allowed by SC-011, and two seconds of zeroed tiles reads as data.
    vi.mocked(fetchDashboard).mockReturnValue(new Promise(() => {}) as never)

    const router = createRouter({ history: createMemoryHistory(), routes })
    await router.push('/admin')
    const wrapper = mountComponent(DashboardView, { global: { plugins: [router] } })
    // The flag is set synchronously by onMounted, but the DOM reflects it one tick later.
    await nextTick()

    expect(wrapper.find('[data-testid="dashboard-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="stat-polls"]').exists()).toBe(false)
  })

  it('says nobody has answered rather than showing three zeros (FR-028 scenario 8)', async () => {
    vi.mocked(fetchDashboard).mockResolvedValue(figures({ yes: 0, maybe: 0, no: 0 }) as never)

    const { wrapper } = await dashboard()

    expect(wrapper.get('[data-testid="distribution-empty"]').text())
      .toBe(de.dashboard.distributionEmpty)
    expect(wrapper.find('[data-testid="distribution-yes"]').exists()).toBe(false)
  })

  it('keeps a real zero in one column as a number', async () => {
    // Zero *maybe* among 23 answers is a finding, not an absence.
    vi.mocked(fetchDashboard).mockResolvedValue(figures({ yes: 20, maybe: 0, no: 3 }) as never)

    const { wrapper } = await dashboard()

    expect(wrapper.find('[data-testid="distribution-empty"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="distribution-maybe"]').text()).toBe('0')
  })

  it('names the next deletion even when none is due this week', async () => {
    vi.mocked(fetchDashboard).mockResolvedValue(
      figures({ deletionsDueSoon: 0, nextDeletion: '2027-01-01T00:00:00Z' }) as never,
    )

    const { wrapper } = await dashboard()

    // "Nothing due this week" and "there is nothing" are different answers.
    expect(wrapper.get('[data-testid="stat-next-deletion"]').text()).toContain('2027')
  })

  it('reports maintenance from the one store the shell also reads (FR-029)', async () => {
    vi.mocked(fetchDashboard).mockResolvedValue(figures() as never)

    const { wrapper } = await dashboard(() => {
      const maintenance = useMaintenanceStore()
      maintenance.enabled = true
      maintenance.since = '2026-09-12T08:00:00Z'
    })

    const tile = wrapper.get('[data-testid="stat-maintenance"]')
    expect(tile.text()).toContain(de.maintenance.on)
    expect(wrapper.get('[data-testid="stat-maintenance-since"]').text()).toContain('September')
  })

  it('holds no control that exists nowhere else (FR-033)', async () => {
    vi.mocked(fetchDashboard).mockResolvedValue(figures() as never)

    const { wrapper } = await dashboard()

    // The dashboard reports; it does not act.
    expect(wrapper.find('[data-testid="maintenance-toggle"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="download-backup"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="poll-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="delete-poll"]').exists()).toBe(false)
  })

  it('sends the operator to sign in when the session is refused (FR-011)', async () => {
    vi.mocked(fetchDashboard).mockRejectedValue({ code: 'unauthorized' })

    const { push } = await dashboard()

    expect(push).toHaveBeenCalledWith(expect.objectContaining({ name: 'sign-in' }))
  })
})

/**
 * Feature 008's region of the dashboard (FR-048a to FR-052).
 *
 * It reads the same payload the wish-list area reads, so agreement between the two screens is
 * structural rather than asserted twice (research R-5). What is asserted here is the part that is
 * this screen's own: what it shows, what it must never show, and that it only reports.
 */
const aWishList = (overrides: Record<string, unknown> = {}) => ({
  id: 'w1',
  title: 'Sommerfest',
  targetDate: '2099-07-18',
  closed: false,
  itemCount: 3,
  entryCount: 3,
  placeCount: 6,
  untakenItemCount: 2,
  completeItemCount: 0,
  listToken: 'tok',
  ...overrides,
})

describe('the dashboard wish-list overview', () => {
  beforeEach(() => {
    vi.mocked(fetchDashboard).mockResolvedValue({
      pollCount: 1,
      responseCount: 1,
      unansweredPolls: 0,
      deletionsDueSoon: 0,
      nextDeletion: null,
      yes: 1,
      maybe: 0,
      no: 0,
    })
    vi.mocked(listWishLists).mockReset()
    vi.mocked(listWishLists).mockResolvedValue([aWishList()])
  })

  it('shows one row per wish list, naming it', async () => {
    // FR-048a, and the narrowing 008 FR-048b made to 007 FR-034.
    const { wrapper } = await dashboard()
    await flush()

    const rows = wrapper.findAll('[data-testid="dashboard-wish-list-row"]')
    expect(rows).toHaveLength(1)
    expect(rows[0].text()).toContain('Sommerfest')
    expect(rows[0].text()).toContain('50')
  })

  it('leads each row into the area in one action, and offers no control of its own', async () => {
    // FR-048d, FR-051: the dashboard reports; it does not act.
    const { wrapper } = await dashboard()
    await flush()

    expect(wrapper.get('[data-testid="dashboard-wish-link-w1"]').attributes('href'))
      .toBe('/admin/wunschlisten/w1')

    const region = wrapper.get('[data-testid="dashboard-wish-lists"]').text()
    expect(region).not.toContain(de.wish.create)
    expect(region).not.toContain(de.wish.deleteList)
  })

  it('never shows a participant name', async () => {
    // FR-050, and the half of 007 FR-034 that the amendment left standing.
    vi.mocked(listWishLists).mockResolvedValue([aWishList({ entryCount: 2 })])

    const { wrapper } = await dashboard()
    await flush()

    expect(wrapper.get('[data-testid="dashboard-wish-lists"]').text()).not.toContain('Anna')
  })

  it('says in words when no wish list exists, rather than showing zeros', async () => {
    // FR-052.
    vi.mocked(listWishLists).mockResolvedValue([])

    const { wrapper } = await dashboard()
    await flush()

    expect(wrapper.find('[data-testid="dashboard-wish-lists-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-wish-list-row"]').exists()).toBe(false)
  })

  it('says the data is unreachable and shows no figure for it', async () => {
    // FR-052 again: a zero here would be a claim about data the system just said it cannot read.
    vi.mocked(listWishLists).mockRejectedValue({ code: 'storage_unavailable' })

    const { wrapper } = await dashboard()
    await flush()

    expect(wrapper.find('[data-testid="dashboard-wish-lists-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="dashboard-wish-list-row"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="dashboard-wish-lists-total"]').exists()).toBe(false)
  })

  it('shows wish lists even when no poll is stored', async () => {
    // The poll figures have their own "nothing yet" state, and it must not swallow this region.
    vi.mocked(fetchDashboard).mockResolvedValue({
      pollCount: 0,
      responseCount: 0,
      unansweredPolls: 0,
      deletionsDueSoon: 0,
      nextDeletion: null,
      yes: 0,
      maybe: 0,
      no: 0,
    })

    const { wrapper } = await dashboard()
    await flush()

    expect(wrapper.find('[data-testid="dashboard-empty"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="dashboard-wish-list-row"]')).toHaveLength(1)
  })
})
