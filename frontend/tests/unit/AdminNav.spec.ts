import { describe, it, expect } from 'vitest'
import { createRouter, createMemoryHistory } from 'vue-router'
import { mountInApp, de } from '../support/mount'
import { routes } from '../../src/router'
import AdminNav from '../../src/components/admin/AdminNav.vue'

/**
 * 007 FR-002, FR-002a, FR-003, FR-004 and FR-014e.
 *
 * Mounted against the real route table rather than a stubbed link, because the property under
 * test is what Vue Router does: it marks the active link with aria-current="page", and we
 * deliberately do not write that attribute ourselves (research.md R-2). A stubbed RouterLink
 * would assert our stub instead of the behaviour a screen reader meets.
 */
async function nav(at: string) {
  const router = createRouter({ history: createMemoryHistory(), routes })
  await router.push(at)
  await router.isReady()

  return mountInApp(AdminNav, { global: { plugins: [router] } })
}

const labels = (wrapper: Awaited<ReturnType<typeof nav>>) =>
  wrapper.findAll('[data-testid^="nav-"]').map((e) => e.text().trim())

describe('Admin navigation', () => {
  it('lists exactly the areas that exist, in order (FR-003, FR-004)', async () => {
    // Five since feature 009 added Ersteller. Still no entry for anything unbuilt: FR-004 forbids
    // a placeholder, and this list is the assertion that none has appeared.
    //
    // Ersteller sits in the middle section rather than inside Einstellungen, because it is a
    // capability of the installation and not a setting of it (009 FR-043).
    const wrapper = await nav('/admin')

    expect(labels(wrapper)).toEqual([
      de.nav.dashboard,
      de.poll.listTitle,
      de.nav.wishLists,
      de.nav.creators,
      de.nav.settings,
    ])
  })

  it('marks the Ersteller entry current on its own address (009 FR-043)', async () => {
    const wrapper = await nav('/admin/ersteller')
    const current = wrapper.findAll('[aria-current="page"]')

    // Exactly one, as everywhere else: two entries carrying it at once is what FR-002 forbids.
    expect(current).toHaveLength(1)
    expect(current[0].text()).toContain(de.nav.creators)
  })

  it('puts settings last, with nothing after it (FR-003, FR-018)', async () => {
    const wrapper = await nav('/admin')
    const ids = wrapper.findAll('[data-testid^="nav-"]').map((e) => e.attributes('data-testid'))

    expect(ids[ids.length - 1]).toBe('nav-settings')
  })

  it('takes its labels from the catalogue, reusing the poll list wording (FR-002a, FR-035)', async () => {
    const wrapper = await nav('/admin')

    // Not a second word for the poll list: "Terminfindungen" already exists as poll.listTitle,
    // and two catalogue entries for one concept is how a rename ends up half-applied.
    expect(wrapper.get('[data-testid="nav-polls"]').text()).toContain(de.poll.listTitle)
  })

  it('leads each entry to its own address (FR-005)', async () => {
    const wrapper = await nav('/admin')

    expect(wrapper.get('[data-testid="nav-dashboard"]').attributes('href')).toBe('/admin')
    expect(wrapper.get('[data-testid="nav-polls"]').attributes('href')).toBe('/admin/terminfindungen')
    expect(wrapper.get('[data-testid="nav-wish-lists"]').attributes('href')).toBe('/admin/wunschlisten')
    expect(wrapper.get('[data-testid="nav-settings"]').attributes('href')).toBe('/admin/einstellungen')
  })

  it('marks exactly one entry as the area currently shown (FR-002)', async () => {
    const wrapper = await nav('/admin/einstellungen')
    const current = wrapper.findAll('[aria-current="page"]')

    expect(current).toHaveLength(1)
    expect(current[0].attributes('data-testid')).toBe('nav-settings')
  })

  it('keeps the poll area current while one poll\'s answers are shown (FR-014e)', async () => {
    const wrapper = await nav('/admin/terminfindungen/some-poll-id')
    const current = wrapper.findAll('[aria-current="page"]')

    // The answers page has an address but no entry of its own; the area it belongs to stays
    // marked, so the navigation never claims the operator has left it.
    expect(current).toHaveLength(1)
    expect(current[0].attributes('data-testid')).toBe('nav-polls')
  })

  it('exposes the navigation as a landmark with a name (FR-013)', async () => {
    const wrapper = await nav('/admin')
    const list = wrapper.get('[data-testid="admin-nav"] .v-list')

    expect(list.attributes('aria-label')).toBe(de.nav.label)
  })
})
