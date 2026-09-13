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
}))

import { fetchDashboard } from '../../src/api/client'
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
