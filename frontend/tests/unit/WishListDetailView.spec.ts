import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  listWishLists: vi.fn(),
  fetchWishList: vi.fn(),
  createWishList: vi.fn(),
  updateWishList: vi.fn(),
  addWishItem: vi.fn(),
  updateWishItem: vi.fn(),
  removeWishItem: vi.fn(),
  deleteWishClaim: vi.fn(),
  deleteWishList: vi.fn(),
}))

import { createRouter, createMemoryHistory } from 'vue-router'
import { fetchWishList, removeWishItem, updateWishItem } from '../../src/api/client'
import { routes } from '../../src/router'
import WishListDetailView from '../../src/components/admin/WishListDetailView.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))
const router = createRouter({ history: createMemoryHistory(), routes })

const aList = (overrides: Record<string, unknown> = {}) => ({
  id: 'w1',
  title: 'Sommerfest',
  description: 'Bitte eintragen',
  targetDate: '2099-07-18',
  closed: false,
  listToken: 'tok',
  entryCount: 2,
  placeCount: 4,
  items: [
    {
      id: 'i1',
      name: 'Kuchen',
      wantedCount: 3,
      position: 0,
      claims: [
        { id: 'c1', displayName: 'Anna' },
        { id: 'c2', displayName: 'Ben' },
      ],
    },
    { id: 'i2', name: 'Grill', wantedCount: 1, position: 1, claims: [] },
  ],
  ...overrides,
})

const mountView = () =>
  mountComponent(WishListDetailView, {
    props: { wishListId: 'w1' },
    global: { plugins: [router] },
  })

describe('WishListDetailView', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
    vi.mocked(fetchWishList).mockReset()
    vi.mocked(updateWishItem).mockReset()
    vi.mocked(removeWishItem).mockReset()
    vi.mocked(fetchWishList).mockResolvedValue(aList())
  })

  it('loads from its own address, so the page survives a reload', async () => {
    // FR-043, SC-008: nothing from the list page is needed to render this.
    const wrapper = mountView()
    await flush()

    expect(vi.mocked(fetchWishList)).toHaveBeenCalledWith('w1')
    expect(wrapper.get('[data-testid="wish-detail-title"]').text()).toBe('Sommerfest')
  })

  it('shows every item with the names on it', async () => {
    const wrapper = mountView()
    await flush()

    expect(wrapper.findAll('[data-testid="wish-detail-item"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="wish-claim-c1"]').text()).toContain('Anna')
  })

  it('says how many entries an item removal destroys, before it happens', async () => {
    // FR-034.
    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="wish-item-remove-i1"]').trigger('click')
    await flush()

    expect(document.body.textContent).toContain('Kuchen')
    expect(document.body.textContent).toContain('2')
    expect(vi.mocked(removeWishItem)).not.toHaveBeenCalled()
  })

  it('shows the refusal when a count is lowered below the entries made, with that number', async () => {
    // FR-033: the message has to name what the operator must deal with first.
    vi.mocked(updateWishItem).mockRejectedValue({ code: 'count_below_entries', limit: 2 })

    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="wish-item-count-i1"] input').setValue('1')
    await wrapper.get('[data-testid="wish-item-count-i1"] input').trigger('change')
    await flush()

    const message = wrapper.get('[data-testid="wish-detail-problem"]').text()
    expect(message).toContain('2')
    expect(message).toContain('Zusagen')
  })

  it('puts a refused count back to what the server still holds', async () => {
    // The field is bound one-way and written back on change, so a refusal would otherwise leave
    // it showing the rejected 1 while the item is still wanted three times - the message and the
    // field disagreeing about the same item.
    vi.mocked(updateWishItem).mockRejectedValue({ code: 'count_below_entries', limit: 2 })

    const wrapper = mountView()
    await flush()

    const field = wrapper.get('[data-testid="wish-item-count-i1"] input')
    await field.setValue('1')
    await field.trigger('change')
    await flush()

    expect((field.element as HTMLInputElement).value).toBe('3')
  })

  it('puts a refused name back to what the server still holds', async () => {
    // FR-031's other half: the rename was refused, so the item is still called Kuchen and the
    // field has to say so.
    vi.mocked(updateWishItem).mockRejectedValue({ code: 'duplicate_item_name', detail: 'Grill' })

    const wrapper = mountView()
    await flush()

    const field = wrapper.get('[data-testid="wish-item-name-i1"] input')
    await field.setValue('Grill')
    await field.trigger('change')
    await flush()

    expect((field.element as HTMLInputElement).value).toBe('Kuchen')
    // FR-008, ui-contract section 4: the refusal names what it collides with.
    expect(wrapper.get('[data-testid="wish-detail-problem"]').text()).toContain('Grill')
  })

  it('marks a closed list and says how to reopen it', async () => {
    // FR-028c: there is no open/close control, because there is no stored state.
    vi.mocked(fetchWishList).mockResolvedValue(aList({ closed: true }))

    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="wish-detail-closed"]').text()).toBe('Geschlossen')
    expect(wrapper.get('[data-testid="wish-detail-closed-hint"]').text()).toContain('Zieldatum')
  })

  it('shows the filled share rounded down', async () => {
    // research R-7.
    vi.mocked(fetchWishList).mockResolvedValue(aList({ entryCount: 999, placeCount: 1000 }))

    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="wish-detail-figures"]').text()).toContain('99')
    expect(wrapper.find('[data-testid="wish-detail-complete"]').exists()).toBe(false)
  })
})
