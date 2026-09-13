import { describe, it, expect } from 'vitest'
import { createRouter, createMemoryHistory } from 'vue-router'
import { routes } from '../../src/router'

/**
 * 007 FR-005, FR-006, FR-007 and research.md R-5: the address table, asserted as a table.
 *
 * These are route *resolutions*, not mounted components. What matters here is that every area has
 * an address of its own, that entering the admin area without naming one lands on the dashboard,
 * and that an address which means nothing still means something definite.
 */
const router = () => createRouter({ history: createMemoryHistory(), routes })

describe('Admin addresses', () => {
  it('gives every area its own address', async () => {
    const r = router()

    for (const [path, name] of [
      ['/admin', 'dashboard'],
      ['/admin/terminfindungen', 'polls'],
      ['/admin/einstellungen', 'settings'],
      ['/admin/anmelden', 'sign-in'],
    ] as const) {
      await r.push(path)
      expect(r.currentRoute.value.name, path).toBe(name)
    }
  })

  it('gives one poll its own address, carrying the poll it names', async () => {
    const r = router()
    await r.push('/admin/terminfindungen/abc-123')

    expect(r.currentRoute.value.name).toBe('poll-answers')
    expect(r.currentRoute.value.params.pollId).toBe('abc-123')
  })

  it('lands on the dashboard when no area is named (FR-006)', async () => {
    const r = router()
    await r.push('/admin')

    expect(r.currentRoute.value.name).toBe('dashboard')
  })

  it('sends an address that matches no area to the dashboard rather than nowhere (FR-007)', async () => {
    const r = router()
    await r.push('/admin/gibt-es-nicht')

    expect(r.currentRoute.value.name).toBe('dashboard')
  })

  it('keeps the sign-in form outside the shell (FR-008)', async () => {
    const r = router()
    await r.push('/admin/anmelden')

    // Every area is a child of the shell route; the sign-in form deliberately is not, so that
    // "no session yet" cannot render a navigation bar listing areas it may not enter.
    const names = r.currentRoute.value.matched.map((m) => m.name)
    expect(names).not.toContain('admin-shell')
  })

  it('leaves the participant addresses untouched (FR-009)', async () => {
    const r = router()

    await r.push('/u/token-123')
    expect(r.currentRoute.value.name).toBe('poll')
    expect(r.currentRoute.value.matched.map((m) => m.name)).not.toContain('admin-shell')

    await r.push('/a/edit-456')
    expect(r.currentRoute.value.name).toBe('response')
    expect(r.currentRoute.value.matched.map((m) => m.name)).not.toContain('admin-shell')
  })
})
