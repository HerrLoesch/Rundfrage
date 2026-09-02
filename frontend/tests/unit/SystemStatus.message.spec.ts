import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import de from '../../src/locales/de.json'

vi.mock('../../src/api/client', () => ({
  fetchMessage: vi.fn(),
  fetchDatabaseStatus: vi.fn(),
}))

import { fetchMessage, fetchDatabaseStatus } from '../../src/api/client'
import SystemStatus from '../../src/components/SystemStatus.vue'

const i18n = createI18n({ legacy: false, locale: 'de', messages: { de } })

function mountComponent() {
  return mount(SystemStatus, { global: { plugins: [i18n] } })
}

describe('SystemStatus - backend message (FR-007)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(fetchDatabaseStatus).mockResolvedValue({
      state: 'reachable',
      checkedAt: '2026-09-02T09:00:00.000Z',
      durationMs: 5,
    })
  })

  it('renders the text the backend returned', async () => {
    vi.mocked(fetchMessage).mockResolvedValue('Rundfrage läuft.')

    const wrapper = mountComponent()
    await new Promise((r) => setTimeout(r, 0))
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="backend-message"]').text()).toContain('Rundfrage läuft.')
  })

  it('reflects a changed backend text without any component change', async () => {
    // FR-007: changing the backend value changes what the page shows.
    vi.mocked(fetchMessage).mockResolvedValue('Ein völlig anderer Text')

    const wrapper = mountComponent()
    await new Promise((r) => setTimeout(r, 0))
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="backend-message"]').text()).toContain(
      'Ein völlig anderer Text',
    )
  })
})
