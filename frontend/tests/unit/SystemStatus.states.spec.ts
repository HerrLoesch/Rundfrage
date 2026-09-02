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

async function mountSettled() {
  const wrapper = mount(SystemStatus, { global: { plugins: [i18n] } })
  await new Promise((r) => setTimeout(r, 0))
  await wrapper.vm.$nextTick()
  return wrapper
}

describe('SystemStatus - three distinguishable states (FR-010)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(fetchMessage).mockResolvedValue('Rundfrage läuft.')
  })

  it('renders the reachable state', async () => {
    vi.mocked(fetchDatabaseStatus).mockResolvedValue({
      state: 'reachable',
      checkedAt: '2026-09-02T09:00:00.000Z',
      durationMs: 10,
    })

    const wrapper = await mountSettled()
    const el = wrapper.get('[data-testid="database-state"]')

    expect(el.attributes('data-state')).toBe('reachable')
    expect(el.text()).toBe(de.status.database.reachable)
  })

  it('renders the unreachable state', async () => {
    vi.mocked(fetchDatabaseStatus).mockResolvedValue({
      state: 'unreachable',
      checkedAt: '2026-09-02T09:00:00.000Z',
      durationMs: 2000,
    })

    const wrapper = await mountSettled()
    const el = wrapper.get('[data-testid="database-state"]')

    expect(el.attributes('data-state')).toBe('unreachable')
    expect(el.text()).toBe(de.status.database.unreachable)
  })

  it('renders the backend-unreachable state distinctly from the database one', async () => {
    vi.mocked(fetchDatabaseStatus).mockRejectedValue(new Error('network down'))

    const wrapper = await mountSettled()
    const el = wrapper.get('[data-testid="database-state"]')

    expect(el.attributes('data-state')).toBe('backendUnreachable')
    expect(el.text()).toBe(de.status.backend.unreachable)
    expect(el.text()).not.toBe(de.status.database.unreachable)
  })

  it('the three states are visually distinguishable from one another', async () => {
    const rendered = new Set<string>()

    for (const setup of [
      () => vi.mocked(fetchDatabaseStatus).mockResolvedValue({ state: 'reachable', checkedAt: 'x', durationMs: 1 }),
      () => vi.mocked(fetchDatabaseStatus).mockResolvedValue({ state: 'unreachable', checkedAt: 'x', durationMs: 1 }),
      () => vi.mocked(fetchDatabaseStatus).mockRejectedValue(new Error('down')),
    ]) {
      setActivePinia(createPinia())
      setup()
      const wrapper = await mountSettled()
      rendered.add(wrapper.get('[data-testid="database-state"]').text())
    }

    expect(rendered.size).toBe(3)
  })
})
