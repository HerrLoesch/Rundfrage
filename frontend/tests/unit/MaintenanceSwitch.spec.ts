import { describe, it, expect, vi } from 'vitest'
import { mountComponent, de } from '../support/mount'
import MaintenanceSwitch from '../../src/components/admin/MaintenanceSwitch.vue'
import { useMaintenanceStore } from '../../src/stores/maintenance'

/**
 * The control that ends a maintenance window is the one that must never be hard to find, and the
 * state it reports must never be inferred from a previous action (FR-025).
 *
 * The asymmetric confirmation is deliberate (ui-contract §2): switching *on* takes participants
 * away and asks first; switching *off* gives them back and does not. A confirmation on the way out
 * would put one more step between an operator and the end of an outage.
 */
/**
 * State is set inside `prepare`, which mount.ts runs after it activates its own pinia and before
 * the component renders. Setting it before mounting would write to a store the component never
 * sees; setting it after would miss the first render.
 */
describe('MaintenanceSwitch (005 FR-025, ui-contract §2)', () => {
  const mountWith = (state: { enabled: boolean; since?: string | null }, spy?: (s: ReturnType<typeof useMaintenanceStore>) => void) => {
    let store!: ReturnType<typeof useMaintenanceStore>
    const wrapper = mountComponent(MaintenanceSwitch, {}, () => {
      store = useMaintenanceStore()
      store.enabled = state.enabled
      store.since = state.since ?? null
      spy?.(store)
    })
    return { wrapper, store }
  }

  it('shows the current state without being asked', () => {
    expect(mountWith({ enabled: false }).wrapper.text()).toContain(de.maintenance.switch)
  })

  it('shows a persistent banner while maintenance is on, not a toast', () => {
    // A toast is missed, and the failure mode of this feature is leaving the site down after the
    // work is finished.
    const { wrapper } = mountWith({ enabled: true })

    expect(wrapper.find('[data-testid="maintenance-banner"]').exists()).toBe(true)
    expect(wrapper.text()).toContain(de.maintenance.bannerTitle)
  })

  it('shows no banner while maintenance is off', () => {
    expect(
      mountWith({ enabled: false }).wrapper.find('[data-testid="maintenance-banner"]').exists(),
    ).toBe(false)
  })

  it('asks before switching on, and does not switch on until confirmed', async () => {
    let set!: ReturnType<typeof vi.spyOn>
    const { wrapper } = mountWith({ enabled: false }, (s) => {
      set = vi.spyOn(s, 'set').mockResolvedValue()
    })

    await wrapper.get('[data-testid="maintenance-toggle"]').trigger('click')

    expect(set).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="maintenance-confirm"]').exists()).toBe(true)

    await wrapper.get('[data-testid="maintenance-confirm-on"]').trigger('click')
    expect(set).toHaveBeenCalledWith(true)
  })

  it('switches off without asking', async () => {
    let set!: ReturnType<typeof vi.spyOn>
    const { wrapper } = mountWith({ enabled: true }, (s) => {
      set = vi.spyOn(s, 'set').mockResolvedValue()
    })

    await wrapper.get('[data-testid="maintenance-toggle"]').trigger('click')

    expect(set).toHaveBeenCalledWith(false)
    expect(wrapper.find('[data-testid="maintenance-confirm"]').exists()).toBe(false)
  })

  it('says since when it was switched on, with the moment in it', () => {
    // Existence alone would pass against an empty element. What the operator needs is the date.
    const { wrapper } = mountWith({ enabled: true, since: '2026-09-05T10:15:00Z' })

    const since = wrapper.get('[data-testid="maintenance-since"]')

    expect(since.text()).toContain('2026')
    expect(since.text()).toContain('September')
  })

  it('says nothing about a moment while maintenance is off', () => {
    expect(mountWith({ enabled: false }).wrapper.find('[data-testid="maintenance-since"]').exists())
      .toBe(false)
  })
})
