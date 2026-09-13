import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  fetchClaims: vi.fn(),
  withdrawClaim: vi.fn(),
}))

import { fetchClaims, withdrawClaim } from '../../src/api/client'
import ClaimView from '../../src/components/wish/ClaimView.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

const aGroup = (overrides: Record<string, unknown> = {}) => ({
  listTitle: 'Sommerfest',
  targetDate: '2099-07-18',
  closed: false,
  entries: [
    { claimId: 'c1', itemName: 'Kuchen', displayName: 'Anna' },
    { claimId: 'c2', itemName: 'Grill', displayName: 'Anna' },
  ],
  ...overrides,
})

const mountView = () => mountComponent(ClaimView, { props: { claimToken: 'ct' } })

describe('ClaimView', () => {
  beforeEach(() => {
    // Dialogs are teleported to document.body and survive their wrapper, so without this a test
    // finds the *previous* test's dialog and clicks a button nothing is listening to.
    document.body.innerHTML = ''
    vi.mocked(fetchClaims).mockReset()
    vi.mocked(withdrawClaim).mockReset()
    vi.mocked(fetchClaims).mockResolvedValue(aGroup())
  })

  it('shows exactly the entries this link covers', async () => {
    // FR-022c: one link per submission, covering its own entries and nothing else.
    const wrapper = mountView()
    await flush()

    expect(wrapper.findAll('[data-testid^="claim-entry-"]')).toHaveLength(2)
    expect(wrapper.get('[data-testid="claim-list-title"]').text()).toBe('Sommerfest')
  })

  it('asks before withdrawing, naming the item and the name', async () => {
    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="claim-withdraw-c1"]').trigger('click')
    await flush()

    const dialog = document.body.textContent ?? ''
    expect(dialog).toContain('Kuchen')
    expect(dialog).toContain('Anna')
    expect(vi.mocked(withdrawClaim)).not.toHaveBeenCalled()
  })

  it('withdraws and says the place is free again', async () => {
    // FR-022a.
    vi.mocked(withdrawClaim).mockResolvedValue(undefined)

    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="claim-withdraw-c1"]').trigger('click')
    await flush()
    const confirm = document.querySelector<HTMLElement>('[data-testid="claim-withdraw-confirmed"]')
    expect(confirm).not.toBeNull()
    confirm!.click()
    // Twice: the click awaits the withdrawal and then re-reads the list.
    await flush()
    await flush()

    expect(vi.mocked(withdrawClaim)).toHaveBeenCalledWith('ct', 'c1')
    expect(wrapper.get('[data-testid="claim-withdrawn"]').text()).toContain('wieder frei')
  })

  it('says the entries are gone, not that the link is unknown, after the last withdrawal', async () => {
    // The server answers the neutral 404 once a link covers nothing (FR-022e), and that is not
    // "your link never existed" - the participant just spent it themselves. Showing the
    // not-found warning here would replace the confirmation they earned with a false alarm.
    vi.mocked(fetchClaims)
      .mockResolvedValueOnce(aGroup({ entries: [{ claimId: 'c1', itemName: 'Kuchen', displayName: 'Anna' }] }))
      .mockRejectedValueOnce({ code: 'not_found' })
    vi.mocked(withdrawClaim).mockResolvedValue(undefined)

    const wrapper = mountView()
    await flush()

    await wrapper.get('[data-testid="claim-withdraw-c1"]').trigger('click')
    await flush()
    document.querySelector<HTMLElement>('[data-testid="claim-withdraw-confirmed"]')!.click()
    await flush()
    await flush()

    expect(wrapper.find('[data-testid="claim-not-found"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="claim-withdrawn"]').text()).toContain('wieder frei')
    expect(wrapper.find('[data-testid="claim-entries-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="claim-entry-c1"]').exists()).toBe(false)
  })

  it('offers no withdrawal on a closed list and says why', async () => {
    // FR-028d: a place freed after closing could no longer be claimed by anybody.
    vi.mocked(fetchClaims).mockResolvedValue(aGroup({ closed: true }))

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="claim-withdraw-c1"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="claim-closed"]').text()).toContain('geschlossen')
    // The entries stay readable.
    expect(wrapper.findAll('[data-testid^="claim-entry-"]')).toHaveLength(2)
  })

  it('names the operator as the way back when the link is lost', async () => {
    // FR-022d.
    const wrapper = mountView()
    await flush()

    expect(wrapper.get('[data-testid="claim-lost-link"]').text()).toContain('angelegt hat')
  })

  it('says one thing for an unknown link and for one whose entries are gone', async () => {
    // FR-022e.
    vi.mocked(fetchClaims).mockRejectedValue({ code: 'not_found' })

    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="claim-not-found"]').exists()).toBe(true)
  })
})
