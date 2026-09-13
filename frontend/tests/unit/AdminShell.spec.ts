import { beforeEach, describe, it, expect, vi } from 'vitest'
import { nextTick } from 'vue'
import { mountInApp, de } from '../support/mount'
import { useMaintenanceStore } from '../../src/stores/maintenance'

vi.mock('../../src/api/client', () => ({
  readMaintenance: vi.fn(async () => ({ enabled: false, since: null })),
  setMaintenance: vi.fn(async () => ({ enabled: false, since: null })),
  signOut: vi.fn(async () => undefined),
  backupUrl: '/api/v1/admin/backup',
}))

const push = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push }),
  useRoute: () => ({ path: '/admin', name: 'dashboard' }),
  RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
  RouterView: { template: '<div data-testid="area">area</div>' },
}))

import AdminShell from '../../src/components/admin/AdminShell.vue'

const shell = (prepare?: () => void) =>
  mountInApp(
    AdminShell,
    {
      global: {
        stubs: {
          RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
          RouterView: { template: '<div data-testid="area">area</div>' },
          AdminNav: { template: '<nav data-testid="admin-nav"></nav>' },
        },
      },
    },
    prepare,
  )

describe('Admin shell', () => {
  beforeEach(() => push.mockClear())

  it('is present and wraps the area it shows (FR-001)', () => {
    const wrapper = shell()

    expect(wrapper.find('[data-testid="admin-shell"]').exists()).toBe(true)
    // The child area renders inside the shell, which is what makes the navigation persist
    // across navigations rather than being re-created by each area.
    expect(wrapper.find('[data-testid="admin-shell"] [data-testid="area"]').exists()).toBe(true)
  })

  it('carries the navigation (FR-002)', () => {
    expect(shell().find('[data-testid="admin-nav"]').exists()).toBe(true)
  })

  it('offers signing out from the shell rather than from an area (FR-010)', () => {
    const wrapper = shell()
    const signOut = wrapper.find('[data-testid="sign-out"]')

    expect(signOut.exists()).toBe(true)
    expect(signOut.text()).toBe(de.shell.signOut)
  })

  it('leads from the wordmark to the dashboard (FR-036)', () => {
    expect(shell().get('[data-testid="brand"]').attributes('href')).toBe('/admin')
  })

  it('shows no maintenance banner while maintenance is off', () => {
    expect(shell().find('[data-testid="maintenance-banner"]').exists()).toBe(false)
  })

  /**
   * FR-026 and FR-026a. The banner belongs to the shell, so it is on screen in every area and
   * its presence depends on the state alone - not on the switch being rendered anywhere. The old
   * arrangement made the banner a child of the switch, which would have carried the warning out
   * of every area the moment the switch moved to settings (research.md R-6).
   */
  it('shows the maintenance banner in every area while maintenance is on (FR-026, FR-026a)', () => {
    const wrapper = shell(() => {
      const maintenance = useMaintenanceStore()
      maintenance.enabled = true
      maintenance.since = '2026-09-12T08:00:00Z'
    })

    const banner = wrapper.find('[data-testid="maintenance-banner"]')
    expect(banner.exists()).toBe(true)
    expect(banner.text()).toContain(de.maintenance.bannerTitle)
    // No MaintenanceSwitch is mounted anywhere in this tree.
    expect(wrapper.find('[data-testid="maintenance-toggle"]').exists()).toBe(false)
  })

  it('states when maintenance was switched on (FR-026)', () => {
    const wrapper = shell(() => {
      const maintenance = useMaintenanceStore()
      maintenance.enabled = true
      maintenance.since = '2026-09-12T08:00:00Z'
    })

    // Existence alone would pass against an empty element. What the operator needs is the date.
    const since = wrapper.get('[data-testid="maintenance-since"]')
    expect(since.text()).toContain('2026')
    expect(since.text()).toContain('September')
  })

  it('clears the banner when maintenance is switched off (FR-026b)', async () => {
    const wrapper = shell(() => {
      const maintenance = useMaintenanceStore()
      maintenance.enabled = true
    })
    expect(wrapper.find('[data-testid="maintenance-banner"]').exists()).toBe(true)

    useMaintenanceStore().enabled = false
    await nextTick()

    expect(wrapper.find('[data-testid="maintenance-banner"]').exists()).toBe(false)
  })

  /**
   * 007 FR-011, carried by the shell rather than by each area.
   *
   * Settings loads nothing of its own, so opening it without a session drew the whole shell -
   * navigation, sign-out and all - around a page the server would refuse. The shell already asks
   * for maintenance state on mount, so it is the one place that notices for every area at once,
   * including any added later that happen to need no data.
   */
  it('sends the operator to sign in when the server refuses the session (FR-011)', async () => {
    const { readMaintenance } = await import('../../src/api/client')
    vi.mocked(readMaintenance).mockRejectedValueOnce({ code: 'unauthorized' })

    shell()
    await new Promise((resolve) => setTimeout(resolve, 0))

    expect(push).toHaveBeenCalledWith(expect.objectContaining({ name: 'sign-in' }))
  })

  it('stays put when the refusal is anything other than the session', async () => {
    const { readMaintenance } = await import('../../src/api/client')
    vi.mocked(readMaintenance).mockRejectedValueOnce({ code: 'storage_unavailable' })

    shell()
    await new Promise((resolve) => setTimeout(resolve, 0))

    // An unreadable store is the area's problem to describe, not a reason to log the operator out.
    expect(push).not.toHaveBeenCalled()
  })
})
