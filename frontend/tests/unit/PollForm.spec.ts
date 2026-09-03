import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent, de } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  signIn: vi.fn(),
  signOut: vi.fn(),
  fetchMessage: vi.fn(),
  fetchDatabaseStatus: vi.fn(),
  listPolls: vi.fn(),
  createPoll: vi.fn(),
}))

import { createPoll } from '../../src/api/client'
import PollForm from '../../src/components/admin/PollForm.vue'

const mountForm = () => mountComponent(PollForm)
const inputIn = (wrapper: ReturnType<typeof mountForm>, testid: string) =>
  wrapper.get(`[data-testid="${testid}"]`).find('input')

async function addDay(wrapper: ReturnType<typeof mountForm>, day: string) {
  await inputIn(wrapper, 'poll-day-input').setValue(day)
  await wrapper.get('[data-testid="poll-add-day"]').trigger('click')
}

describe('PollForm (FR-008 to FR-016)', () => {
  beforeEach(() => vi.clearAllMocks())

  it('offers a title, a message and a way to add days', () => {
    const wrapper = mountForm()

    expect(inputIn(wrapper, 'poll-title').exists()).toBe(true)
    expect(wrapper.get('[data-testid="poll-message"]').find('textarea').exists()).toBe(true)
    expect(inputIn(wrapper, 'poll-day-input').exists()).toBe(true)
    expect(wrapper.find('[data-testid="poll-add-day"]').exists()).toBe(true)
  })

  it('lists an added day and stores a repeated day only once', async () => {
    // FR-012, visible before the request is even sent.
    const wrapper = mountForm()

    await addDay(wrapper, '2026-10-15')
    await addDay(wrapper, '2026-10-15')

    expect(wrapper.findAll('[data-testid="poll-day"]')).toHaveLength(1)
  })

  it('shows days chronologically regardless of the order they were added', async () => {
    // FR-013
    const wrapper = mountForm()

    for (const day of ['2026-10-20', '2026-10-15', '2026-10-17']) {
      await addDay(wrapper, day)
    }

    const shown = wrapper.findAll('[data-testid="poll-day"]').map((d) => d.attributes('data-date'))
    expect(shown).toEqual(['2026-10-15', '2026-10-17', '2026-10-20'])
  })

  it('translates a server-side limit refusal and names the limit', async () => {
    // The contract returns a code; the frontend owns the words (FR-029, FR-015).
    vi.mocked(createPoll).mockRejectedValue({ code: 'title_too_long', limit: 300 })

    const wrapper = mountForm()
    await inputIn(wrapper, 'poll-title').setValue('x')
    await addDay(wrapper, '2026-10-15')
    await wrapper.get('[data-testid="poll-form"]').trigger('submit')
    await new Promise((r) => setTimeout(r, 0))

    expect(wrapper.get('[data-testid="poll-error"]').text()).toContain('300')
  })

  it('shows the participant link and the retention deadline after creating', async () => {
    // FR-016 and FR-039a: the link to share, and when it will disappear.
    vi.mocked(createPoll).mockResolvedValue({
      id: 'p1',
      title: 'Grillabend',
      participantToken: 'abcdefghijklmnopqrstuv',
      retentionDeadline: '2026-11-16T22:00:00Z',
      responseCount: 0,
      dayCount: 1,
    })

    const wrapper = mountForm()
    await inputIn(wrapper, 'poll-title').setValue('Grillabend')
    await addDay(wrapper, '2026-10-15')
    await wrapper.get('[data-testid="poll-form"]').trigger('submit')
    await new Promise((r) => setTimeout(r, 0))

    expect(wrapper.get('[data-testid="poll-share-link"]').text()).toContain('abcdefghijklmnopqrstuv')
    expect(wrapper.get('[data-testid="poll-retention"]').text()).toContain(de.poll.retention)
  })
})
