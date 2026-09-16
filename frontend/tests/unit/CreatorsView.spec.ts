import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  listCreators: vi.fn(),
  createCreator: vi.fn(),
  renameCreator: vi.fn(),
  reissueCreatorLink: vi.fn(),
  revokeCreatorLink: vi.fn(),
  deleteCreator: vi.fn(),
}))

import { createRouter, createMemoryHistory } from 'vue-router'
import { listCreators } from '../../src/api/client'
import { routes } from '../../src/router'
import CreatorsView from '../../src/components/admin/CreatorsView.vue'
import de from '../../src/locales/de.json'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))
const router = createRouter({ history: createMemoryHistory(), routes })
const mountView = () => mountComponent(CreatorsView, { global: { plugins: [router] } })

/**
 * v-dialog teleports its content to the document body, so it is outside the wrapper's tree.
 * Asserting through the wrapper silently found nothing and read as "the dialog never opened".
 */
const inOverlay = (testid: string) => document.body.querySelector(`[data-testid="${testid}"]`)

const aCreator = (overrides: Record<string, unknown> = {}) => ({
  id: 'c1',
  name: 'Anna',
  createdAt: '2026-09-01T10:00:00Z',
  hasLink: true,
  linkToken: 'abcdefghijklmnopqrstuv',
  pollCount: 2,
  wishListCount: 3,
  ...overrides,
})

beforeEach(() => {
  vi.mocked(listCreators).mockReset()
  // Overlays outlive their wrapper, so a dialog from the previous test would still be in the body.
  document.body.innerHTML = ''
})

describe('the Ersteller area', () => {
  it('shows every field of a row (FR-044)', async () => {
    vi.mocked(listCreators).mockResolvedValue([aCreator()])
    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="creator-name"]').text()).toBe('Anna')
    expect(wrapper.find('[data-testid="creator-poll-count"]').text()).toContain('2')
    expect(wrapper.find('[data-testid="creator-wish-list-count"]').text()).toContain('3')
    expect(wrapper.find('[data-testid="creator-link"]').text()).toContain('abcdefghijklmnopqrstuv')
    expect(wrapper.find('[data-testid="creator-created"]').exists()).toBe(true)
  })

  it('states a revoked link in words and still shows the counts (FR-044a, FR-057)', async () => {
    // Not hidden and not merely greyed out: the operator's next decision - new link, or delete -
    // needs those figures, and colour alone is not a statement.
    vi.mocked(listCreators).mockResolvedValue([
      aCreator({ hasLink: false, linkToken: null }),
    ])
    const wrapper = mountView()
    await flush()

    const noLink = wrapper.find('[data-testid="creator-no-link"]')
    expect(noLink.exists()).toBe(true)
    expect(noLink.text()).toBe(de.creator.noLink)

    expect(wrapper.find('[data-testid="creator-link"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="creator-poll-count"]').text()).toContain('2')
  })

  it('opens on the list, with creating revealed on demand (FR-045)', async () => {
    vi.mocked(listCreators).mockResolvedValue([aCreator()])
    const wrapper = mountView()
    await flush()

    expect(wrapper.find('[data-testid="creator-form"]').exists()).toBe(false)

    await wrapper.find('[data-testid="creator-create-open"]').trigger('click')
    expect(wrapper.find('[data-testid="creator-form"]').exists()).toBe(true)
  })

  it('distinguishes "none exist" from "cannot be read", and shows neither as a zero (FR-046, SC-012)', async () => {
    vi.mocked(listCreators).mockResolvedValue([])
    const empty = mountView()
    await flush()

    expect(empty.find('[data-testid="creator-empty"]').exists()).toBe(true)
    expect(empty.find('[data-testid="creator-unavailable"]').exists()).toBe(false)
    expect(empty.find('[data-testid="creator-create-empty"]').exists()).toBe(true)
    expect(empty.text()).not.toContain('0')

    vi.mocked(listCreators).mockRejectedValue({ code: 'unexpected' })
    const broken = mountView()
    await flush()

    expect(broken.find('[data-testid="creator-unavailable"]').exists()).toBe(true)
    expect(broken.find('[data-testid="creator-empty"]').exists()).toBe(false)
    expect(broken.find('[data-testid="creator-list"]').exists()).toBe(false)
  })
})

describe('revoking and deleting are unmistakably different (FR-020b)', () => {
  it('offers them as separate controls that are not adjacent', async () => {
    vi.mocked(listCreators).mockResolvedValue([aCreator()])
    const wrapper = mountView()
    await flush()

    const order = wrapper
      .findAll('[data-testid^="creator-"]')
      .map((e) => e.attributes('data-testid'))
      .filter((id) => ['creator-rename', 'creator-reissue', 'creator-revoke', 'creator-delete'].includes(id!))

    expect(order).toEqual(['creator-rename', 'creator-reissue', 'creator-revoke', 'creator-delete'])

    // The two that matter are separated by at least one other control, so a mis-click on one
    // cannot land on the other.
    expect(order.indexOf('creator-delete') - order.indexOf('creator-revoke')).toBe(1)
  })

  it('says revoking removes nothing, and deleting says how much it destroys', async () => {
    vi.mocked(listCreators).mockResolvedValue([aCreator()])
    const wrapper = mountView()
    await flush()

    await wrapper.find('[data-testid="creator-revoke"]').trigger('click')
    await flush()
    const revoke = inOverlay('creator-revoke-confirm-body')!.textContent ?? ''

    // The substance, not just different wording: the revoke text promises survival.
    expect(revoke).toContain('2')
    expect(revoke).toContain('3')
    expect(revoke).toContain('bleiben erhalten')

    await wrapper.find('[data-testid="creator-delete"]').trigger('click')
    await flush()
    const remove = inOverlay('creator-delete-confirm-body')!.textContent ?? ''

    // The delete text names both counts before anything happens (FR-020a) and says an export must
    // come first if anything is to be kept (FR-020c).
    expect(remove).toContain('2')
    expect(remove).toContain('3')
    expect(remove).not.toContain('bleiben erhalten')
    expect(inOverlay('creator-delete-confirm-note')!.textContent!.trim())
      .toBe(de.creator.deleteKeepHint)
  })

  it('announces destruction only on the deletion', async () => {
    vi.mocked(listCreators).mockResolvedValue([aCreator()])
    const wrapper = mountView()
    await flush()

    await wrapper.find('[data-testid="creator-revoke"]').trigger('click')
    await flush()

    expect(inOverlay('creator-revoke-confirm')).not.toBeNull()
    expect(inOverlay('creator-delete-confirm')).toBeNull()
  })
})

describe('the cap of a hundred', () => {
  it('says that a place is freed by deleting, not by revoking (FR-009a)', async () => {
    // The refusal an operator at the limit actually meets. Without the second sentence they
    // revoke links to make room and get nowhere, with nothing explaining why.
    expect(de.error.creator_limit_reached).toContain('gelöscht')
    expect(de.error.creator_limit_reached).toContain('Sperren')
  })
})
