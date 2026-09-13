import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createRouter, createMemoryHistory } from 'vue-router'
import { mountComponent } from '../support/mount'

/**
 * The whole client surface the *reachable* routes need, not only what this component calls.
 *
 * The redirect tests below navigate to the poll list and to the sign-in form, and Vue Router
 * imports those components lazily. A mock missing an export they import throws at import time,
 * the navigation rejects, and the test reads as "the component did not redirect" - which is a
 * misleading way to be told the mock is incomplete.
 */
vi.mock('../../src/api/client', () => ({
  fetchPollResults: vi.fn(),
  deleteResponse: vi.fn(),
  deletePoll: vi.fn(),
  listPolls: vi.fn(async () => []),
  createPoll: vi.fn(),
  importPoll: vi.fn(),
  signIn: vi.fn(),
  signOut: vi.fn(),
  exportUrl: (pollId: string) => `/api/v1/admin/polls/${pollId}/export`,
  backupUrl: '/api/v1/admin/backup',
}))

import { deleteResponse, fetchPollResults } from '../../src/api/client'
import { routes } from '../../src/router'
import PollAnswersView from '../../src/components/admin/PollAnswersView.vue'

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

/**
 * The redirects are asserted through a spy on push rather than through the router's final state.
 *
 * Mounting a route's component directly while also driving the same router means two navigations
 * race, and vue-router cancels one with "Navigation cancelled ... with a new navigation" - so the
 * final currentRoute reported neither destination and the test read as "it did not redirect".
 * Where the operator is sent is the requirement (FR-014g, FR-011); which of two concurrent
 * navigations a test harness wins is not.
 */

const aPollView = (responses: unknown[] = [{ id: 'r1', displayName: 'Ada', answers: [] }]) => ({
  title: 'Grillabend',
  message: 'Kurze Nachricht',
  days: [{ id: 'd1', date: '2027-01-01' }],
  totals: [{ dayId: 'd1', yes: 1, maybe: 0, no: 0 }],
  responses,
  page: 1,
  pageCount: 1,
  responseCount: responses.length,
})

async function answers(pollId = 'p1') {
  const router = createRouter({ history: createMemoryHistory(), routes })
  await router.push(`/admin/terminfindungen/${pollId}`)
  await router.isReady()

  const push = vi.spyOn(router, 'push').mockResolvedValue(undefined)

  const wrapper = mountComponent(PollAnswersView, {
    props: { pollId },
    global: { plugins: [router] },
  })
  await flush()

  return { wrapper, router, push }
}

/**
 * 007 FR-014a, FR-014c, FR-014d, FR-014f, FR-014g.
 *
 * One poll's answers as a destination rather than a disclosure.
 */
describe("One poll's answers", () => {
  beforeEach(() => vi.clearAllMocks())

  it('loads from the address it was given, not from anything handed over (FR-014a)', async () => {
    vi.mocked(fetchPollResults).mockResolvedValue(aPollView() as never)

    await answers('p9')

    // A pasted link must behave exactly like a clicked one, and it starts at the first page.
    expect(fetchPollResults).toHaveBeenCalledWith('p9', 1)
  })

  it('carries the poll title and message (FR-014c)', async () => {
    vi.mocked(fetchPollResults).mockResolvedValue(aPollView() as never)

    const { wrapper } = await answers()

    const page = wrapper.get('[data-testid="poll-answers"]')
    expect(page.text()).toContain('Grillabend')
    expect(page.text()).toContain('Kurze Nachricht')
  })

  it('shows the grid, with responses deletable (FR-014c, FR-016)', async () => {
    vi.mocked(fetchPollResults).mockResolvedValue(aPollView() as never)

    const { wrapper } = await answers()

    expect(wrapper.find('[data-testid="result-grid"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="delete-response"]').exists()).toBe(true)
  })

  it('offers a way back to the list (FR-014c)', async () => {
    vi.mocked(fetchPollResults).mockResolvedValue(aPollView() as never)

    const { wrapper } = await answers()

    expect(wrapper.get('[data-testid="back-to-polls"]').attributes('href'))
      .toBe('/admin/terminfindungen')
  })

  it('does not repeat the export or the poll deletion (FR-014d)', async () => {
    vi.mocked(fetchPollResults).mockResolvedValue(aPollView() as never)

    const { wrapper } = await answers()

    // They are one action away on the list. Two places would be two things to keep in step.
    expect(wrapper.find('[data-testid="export-poll"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="delete-poll"]').exists()).toBe(false)
  })

  it('stays here showing the empty state when the last answer is deleted (FR-014f)', async () => {
    vi.mocked(fetchPollResults)
      .mockResolvedValueOnce(aPollView() as never)
      .mockResolvedValueOnce(aPollView([]) as never)
    vi.mocked(deleteResponse).mockResolvedValue(undefined as never)

    const { wrapper, push } = await answers('p1')

    await wrapper.get('[data-testid="delete-response"]').trigger('click')
    await flush()

    // Nowhere was navigated to: the operator stays with the poll they were reading.
    expect(push).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="results-empty"]').exists()).toBe(true)
  })

  it('sends the operator to the list when the poll is not there (FR-014g)', async () => {
    vi.mocked(fetchPollResults).mockRejectedValue({ code: 'not_found' })

    const { push } = await answers('weg')

    // Not the dashboard, and not an empty grid that would read as "nobody answered".
    expect(push).toHaveBeenCalledWith(expect.objectContaining({ name: 'polls' }))
  })

  it('sends the operator to sign in when the session is refused (FR-011)', async () => {
    vi.mocked(fetchPollResults).mockRejectedValue({ code: 'unauthorized' })

    const { push } = await answers()

    expect(push).toHaveBeenCalledWith(expect.objectContaining({ name: 'sign-in' }))
  })

  it('says the data is unreachable rather than showing an empty poll (FR-017)', async () => {
    vi.mocked(fetchPollResults).mockRejectedValue({ code: 'storage_unavailable' })

    const { wrapper } = await answers()

    expect(wrapper.find('[data-testid="storage-unavailable"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="poll-answers"]').exists()).toBe(false)
  })

  /**
   * 002 research R-7 and 004's UI contract: the rows are paged fifty at a time on the server, so
   * the address has to be able to ask for a page. Without this the creator of a poll at the
   * 1000-response limit could read fifty answers and no more.
   */
  it('re-reads the poll when the grid asks for another page', async () => {
    vi.mocked(fetchPollResults).mockResolvedValue({
      ...aPollView(),
      page: 1,
      pageCount: 3,
      responseCount: 150,
    } as never)

    const { wrapper } = await answers('p1')
    vi.mocked(fetchPollResults).mockClear()

    await wrapper.findComponent({ name: 'ResultGrid' }).vm.$emit('changePage', 2)
    await flush()

    expect(fetchPollResults).toHaveBeenCalledWith('p1', 2)
  })
})
