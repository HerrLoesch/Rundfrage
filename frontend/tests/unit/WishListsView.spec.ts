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
import { listWishLists } from '../../src/api/client'
import { routes } from '../../src/router'
import WishListsView from '../../src/components/admin/WishListsView.vue'
import { filledPercent, isComplete } from '../../src/stores/wishLists'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))
const router = createRouter({ history: createMemoryHistory(), routes })
const mountView = () => mountComponent(WishListsView, { global: { plugins: [router] } })

const aRow = (overrides: Record<string, unknown> = {}) => ({
  id: 'w1',
  title: 'Sommerfest',
  targetDate: '2099-07-18',
  closed: false,
  itemCount: 3,
  entryCount: 3,
  placeCount: 6,
  untakenItemCount: 2,
  completeItemCount: 0,
  listToken: 'tok',
  ...overrides,
})

describe('the filled share', () => {
  it('rounds down, so one name short never reads as finished', () => {
    // research R-7.
    expect(filledPercent(999, 1000)).toBe(99)
    expect(filledPercent(1000, 1000)).toBe(100)
    expect(filledPercent(3, 6)).toBe(50)
    expect(filledPercent(0, 0)).toBe(0)
  })

  it('decides completeness from the counts, never from the percentage', () => {
    // FR-045b: the word and the number beside it cannot disagree.
    expect(isComplete(999, 1000)).toBe(false)
    expect(isComplete(1000, 1000)).toBe(true)
    expect(isComplete(0, 0)).toBe(false)
  })
})

describe('WishListsView', () => {
  beforeEach(() => {
    vi.mocked(listWishLists).mockReset()
    vi.mocked(listWishLists).mockResolvedValue([aRow()])
  })

  it('shows each list with its figures, both readings labelled separately', async () => {
    // FR-045, FR-045a.
    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="wish-entries-w1"]').text()).toContain('50')
    expect(wrapper.get('[data-testid="wish-untaken-w1"]').text()).toContain('2')
  })

  it('says "complete" in words rather than leaving it at 100 %', async () => {
    // FR-045b.
    vi.mocked(listWishLists).mockResolvedValue([aRow({ entryCount: 6, completeItemCount: 3 })])

    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="wish-complete-w1"]').text()).toBe('Vollständig')
  })

  it('marks a closed list in words', async () => {
    vi.mocked(listWishLists).mockResolvedValue([aRow({ closed: true })])

    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="wish-closed-w1"]').text()).toBe('Geschlossen')
  })

  it('offers creating directly from the empty state', async () => {
    // FR-042a.
    vi.mocked(listWishLists).mockResolvedValue([])

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="wish-lists-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="wish-create-empty"]').exists()).toBe(true)
  })

  it('distinguishes "nothing stored" from "cannot be read", and shows no zeros for either', async () => {
    // FR-046.
    vi.mocked(listWishLists).mockRejectedValue({ code: 'storage_unavailable' })

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="wish-lists-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="wish-lists-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="wish-list-row"]').exists()).toBe(false)
  })

  it('keeps the creation form closed until it is asked for', async () => {
    // FR-042: a list half-written yesterday must not be one click from being created today.
    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="wish-list-form"]').exists()).toBe(false)

    await wrapper.get('[data-testid="wish-create-toggle"]').trigger('click')
    expect(wrapper.find('[data-testid="wish-list-form"]').exists()).toBe(true)

    await wrapper.get('[data-testid="wish-create-toggle"]').trigger('click')
    expect(wrapper.find('[data-testid="wish-list-form"]').exists()).toBe(false)
  })

  it('contains no installation-wide control', async () => {
    // FR-047. The area shows what serves a wish list and nothing that configures the
    // installation - the separation is the point of the shell, so it is asserted, not assumed.
    const wrapper = mountView()
    await flush()

    const html = wrapper.html()
    for (const forbidden of ['maintenance', 'backup', 'restore', 'Wartung', 'Sicherung']) {
      expect(html).not.toContain(forbidden)
    }
  })
})
