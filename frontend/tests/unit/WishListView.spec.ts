import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  fetchWishListByToken: vi.fn(),
  claimWishItems: vi.fn(),
}))

import { claimWishItems, fetchWishListByToken } from '../../src/api/client'
import WishListView from '../../src/components/wish/WishListView.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

const aList = (overrides: Record<string, unknown> = {}) => ({
  title: 'Sommerfest',
  description: 'Bitte eintragen',
  targetDate: '2099-07-18',
  closed: false,
  items: [
    { id: 'i1', name: 'Kuchen', wantedCount: 3, openPlaces: 2, names: ['Anna'] },
    { id: 'i2', name: 'Grill', wantedCount: 1, openPlaces: 0, names: ['Ben'] },
  ],
  ...overrides,
})

const mountView = () => mountComponent(WishListView, { props: { listToken: 'tok' } })

describe('WishListView', () => {
  beforeEach(() => {
    // Both mocks, not only one: call counts are asserted below, and a module mock is shared
    // across tests in a file.
    vi.mocked(fetchWishListByToken).mockReset()
    vi.mocked(claimWishItems).mockReset()
    vi.mocked(fetchWishListByToken).mockResolvedValue(aList())
  })

  it('shows the list from a bare link, with no step in front of the form', async () => {
    // Principle I, FR-014: the page that loads is the page that takes the entry.
    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="wish-view-title"]').text()).toBe('Sommerfest')
    expect(wrapper.find('[data-testid="wish-claim-form"]').exists()).toBe(true)
  })

  it('says how many places are open, in words', async () => {
    // FR-018, FR-057: readable without colour.
    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="wish-open-i1"]').text()).toContain('2')
  })

  it('marks a complete item in words and offers it no entry control', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="wish-complete-i2"]').text()).toBe('Vollständig')
    expect(wrapper.find('[data-testid="wish-choose-i2"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="wish-choose-i1"]').exists()).toBe(true)
  })

  it('states the visibility of the name before the name field', async () => {
    // FR-019a.
    const wrapper = mountView()
    await flush()

    const html = wrapper.get('[data-testid="wish-claim-form"]').html()
    expect(html.indexOf('data-testid="wish-visibility-notice"'))
      .toBeLessThan(html.indexOf('data-testid="wish-name"'))
  })

  it('asks for the name before it asks what you bring', async () => {
    // The order of the question, and the order of the document. Ticking boxes first and then
    // scrolling past the whole list to find a name field is not how the sentence goes.
    const wrapper = mountView()
    await flush()

    const html = wrapper.get('[data-testid="wish-claim-form"]').html()

    expect(html.indexOf('data-testid="wish-name"'))
      .toBeLessThan(html.indexOf('data-testid="wish-items"'))
    // And the submit control comes after both, not between them.
    expect(html.indexOf('data-testid="wish-items"'))
      .toBeLessThan(html.indexOf('data-testid="wish-submit"'))
  })

  it('numbers the three things it asks for, and the numbers are decorative', async () => {
    // The digits repeat what the headings already say, so a screen reader is spared "one Wie
    // heißt du" - the order it needs is carried by the document (FR-056).
    const wrapper = mountView()
    await flush()

    const steps = wrapper.findAll('.rf-step__number')
    expect(steps).toHaveLength(3)
    expect(steps.map((step) => step.text())).toEqual(['1', '2', '3'])
    expect(steps.every((step) => step.attributes('aria-hidden') === 'true')).toBe(true)
  })

  it('updates the open places after a claim without a manual reload', async () => {
    // FR-021.
    const claimed = aList({
      items: [
        { id: 'i1', name: 'Kuchen', wantedCount: 3, openPlaces: 1, names: ['Anna', 'Chris'] },
        { id: 'i2', name: 'Grill', wantedCount: 1, openPlaces: 0, names: ['Ben'] },
      ],
    })
    vi.mocked(claimWishItems).mockResolvedValue({ claimToken: 'ct', list: claimed })

    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="wish-choose-i1"] input').setValue(true)
    await wrapper.get('[data-testid="wish-name"] input').setValue('Chris')
    // The button is the form's submit control, so submitting the form is what clicking it does
    // in a browser. jsdom does not run that default action for a synthetic click, and the
    // end-to-end suite covers the real click.
    await wrapper.get('[data-testid="wish-claim-form"]').trigger('submit')
    await flush()

    expect(wrapper.get('[data-testid="wish-open-i1"]').text()).toContain('1')
    expect(vi.mocked(fetchWishListByToken)).toHaveBeenCalledTimes(1)
  })

  it('hands over the personal link and says to keep it', async () => {
    // FR-022.
    vi.mocked(claimWishItems).mockResolvedValue({ claimToken: 'ct', list: aList() })

    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="wish-choose-i1"] input').setValue(true)
    await wrapper.get('[data-testid="wish-name"] input').setValue('Chris')
    // The button is the form's submit control, so submitting the form is what clicking it does
    // in a browser. jsdom does not run that default action for a synthetic click, and the
    // end-to-end suite covers the real click.
    await wrapper.get('[data-testid="wish-claim-form"]').trigger('submit')
    await flush()

    expect(wrapper.get('[data-testid="claim-url"]').text()).toContain('/z/ct')
    expect(wrapper.get('[data-testid="claim-hint"]').text()).toContain('bewahre ihn auf')
  })

  it('offers no entry form on a closed list and says why', async () => {
    // FR-028b: the form is honestly absent rather than present and refusing.
    vi.mocked(fetchWishListByToken).mockResolvedValue(aList({ closed: true }))

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="wish-claim-form"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="wish-closed"]').text()).toContain('geschlossen')
    // Everything it showed is still shown.
    expect(wrapper.get('[data-testid="wish-items"]').text()).toContain('Kuchen')
  })

  it('says one thing for unknown, malformed and deleted links alike', async () => {
    // SC-012.
    vi.mocked(fetchWishListByToken).mockRejectedValue({ code: 'not_found' })

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="wish-not-found"]').exists()).toBe(true)
  })

  it('shows the maintenance notice rather than the list', async () => {
    // FR-025.
    vi.mocked(fetchWishListByToken).mockRejectedValue({ code: 'maintenance' })

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="wish-view-title"]').exists()).toBe(false)
    expect(wrapper.html()).toContain('Wartung')
  })

  it('re-reads the list when a claim is refused, so the page stops showing a stale state', async () => {
    // FR-017b.
    vi.mocked(claimWishItems).mockRejectedValue({ code: 'item_full' })

    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="wish-choose-i1"] input').setValue(true)
    await wrapper.get('[data-testid="wish-name"] input').setValue('Chris')
    // The button is the form's submit control, so submitting the form is what clicking it does
    // in a browser. jsdom does not run that default action for a synthetic click, and the
    // end-to-end suite covers the real click.
    await wrapper.get('[data-testid="wish-claim-form"]').trigger('submit')
    await flush()

    expect(wrapper.get('[data-testid="wish-problem"]').text()).toContain('eingetragen')
    expect(vi.mocked(fetchWishListByToken)).toHaveBeenCalledTimes(2)
  })
  it('gives every control a name and leaves none reachable by pointer only', async () => {
    // FR-056, FR-057. Asserted rather than eyeballed: an icon-only control and a state carried
    // by colour are both invisible to somebody who cannot see or cannot point.
    const wrapper = mountView()
    await flush()

    // The selection control says which item it selects.
    expect(wrapper.get('[data-testid="wish-choose-i1"] input').attributes('aria-label'))
      .toContain('Kuchen')

    // The submit control carries text, not an icon alone.
    expect(wrapper.get('[data-testid="wish-submit"]').text().trim().length).toBeGreaterThan(0)

    // Every control is a real focusable element - or contains one - so keyboard operation needs
    // no extra wiring. Vuetify puts the test id on the button itself and on the *wrapper* of a
    // field, which is why both cases are accepted here.
    for (const testId of ['wish-choose-i1', 'wish-name', 'wish-submit']) {
      const element = wrapper.get(`[data-testid="${testId}"]`)
      const isItself = ['INPUT', 'BUTTON', 'TEXTAREA', 'A'].includes(element.element.tagName)

      expect(isItself || element.find('input, button, textarea, a').exists()).toBe(true)
    }

    // The complete state is text, and removing every colour leaves it readable.
    expect(wrapper.get('[data-testid="wish-complete-i2"]').text()).toBe('Vollständig')
  })

})
