import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountInApp, de } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  readMaintenance: vi.fn(async () => ({ enabled: false, since: null })),
  setMaintenance: vi.fn(async () => ({ enabled: false, since: null })),
  previewRestore: vi.fn(),
  restoreBackup: vi.fn(),
  backupUrl: '/api/v1/admin/backup',
}))

vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }))

import SettingsView from '../../src/components/admin/SettingsView.vue'

const settings = () => mountInApp(SettingsView)

/**
 * 007 US1 and US2. What configures or maintains the installation lives here and nowhere else, and
 * the poll area holds none of it (FR-015, FR-019, SC-002).
 */
describe('Settings area', () => {
  beforeEach(() => vi.clearAllMocks())

  it('holds maintenance mode, the backup download and the restore (FR-019)', () => {
    const wrapper = settings()

    expect(wrapper.find('[data-testid="settings-maintenance"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="settings-backup"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="settings-restore"]').exists()).toBe(true)
  })

  it('carries the controls those sections exist for', () => {
    const wrapper = settings()

    expect(wrapper.find('[data-testid="maintenance-toggle"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="download-backup"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="restore-file"]').exists()).toBe(true)
  })

  /**
   * The safety half of this feature. Import belongs to the poll area because it *makes* a poll;
   * keeping it out of here is what puts "add one poll" and "replace every poll" in different
   * areas, which enforces 005 FR-001 more strongly than 005 itself could (FR-019, FR-025).
   */
  it('holds no poll control, and in particular not the import (FR-019, FR-025, SC-002)', () => {
    const wrapper = settings()

    expect(wrapper.find('[data-testid="import-panel"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="poll-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="poll-list-item"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="export-poll"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="delete-poll"]').exists()).toBe(false)
  })

  it('does not render the maintenance banner; the shell does (FR-026a)', () => {
    // Otherwise the settings page would show the warning twice - once from the shell above it and
    // once from the switch inside it (research.md R-6).
    expect(settings().find('[data-testid="maintenance-banner"]').exists()).toBe(false)
  })

  // --- US2 ------------------------------------------------------------------------------------

  it('names and describes each section (FR-020)', () => {
    const wrapper = settings()

    for (const [testid, title, body] of [
      ['settings-maintenance', de.settings.maintenanceTitle, de.settings.maintenanceBody],
      ['settings-backup', de.settings.backupTitle, de.settings.backupBody],
      ['settings-restore', de.settings.restoreTitle, de.settings.restoreBody],
    ] as const) {
      const section = wrapper.get(`[data-testid="${testid}"]`)
      expect(section.text()).toContain(title)
      expect(section.text()).toContain(body)
    }
  })

  it('uses no modal dialog (FR-020)', () => {
    // The project decided this once already: MaintenanceSwitch confirms inline because the
    // operator is looking at the control, and a second pattern for the same kind of decision
    // would contradict it.
    expect(settings().find('.v-overlay').exists()).toBe(false)
  })

  it('orders the sections by consequence, restore last and set apart (FR-020a)', () => {
    const order = settings()
      .findAll('[data-testid^="settings-"]')
      .map((s) => s.attributes('data-testid'))

    expect(order).toEqual(['settings-maintenance', 'settings-backup', 'settings-restore'])
    expect(settings().get('[data-testid="settings-restore"]').classes()).toContain('settings-grave')
  })
})
