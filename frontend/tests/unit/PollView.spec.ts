import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent, de } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  fetchPoll: vi.fn(),
  submitResponse: vi.fn(),
  fetchOwnResponse: vi.fn(),
  reviseResponse: vi.fn(),
}))

import {
  fetchOwnResponse,
  fetchPoll,
  reviseResponse,
  submitResponse,
} from '../../src/api/client'
import PollView from '../../src/components/poll/PollView.vue'
import { useAnsweringStore } from '../../src/stores/answering'

const POLL = {
  title: 'Grillabend',
  message: null,
  days: [{ id: 'day-1', date: '2026-11-18' }],
  totals: [{ dayId: 'day-1', yes: 1, maybe: 0, no: 0 }],
  responses: [{ id: 'r1', displayName: 'Anna', answers: [{ dayId: 'day-1', availability: 'yes' }] }],
  page: 1,
  pageCount: 1,
  responseCount: 1,
}

const settle = () => new Promise((r) => setTimeout(r, 0))

describe('PollView - submitting, then saving again', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(fetchPoll).mockResolvedValue(POLL as never)
    vi.mocked(submitResponse).mockResolvedValue({ responseId: 'r1', editToken: 'tok-1' })
    vi.mocked(reviseResponse).mockResolvedValue({
      responseId: 'r1',
      displayName: 'Anna',
      answers: [],
      poll: POLL,
    } as never)
    vi.mocked(fetchOwnResponse).mockResolvedValue({
      responseId: 'r1',
      displayName: 'Anna',
      answers: [{ dayId: 'day-1', availability: 'yes' }],
      poll: POLL,
    } as never)
  })

  it('offers submission before anything has been sent', async () => {
    const wrapper = mountComponent(PollView, { props: { pollToken: 'poll-token' } })
    await settle()
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="answer-submit"]').text()).toContain(de.participate.submit)
  })

  it('switches to revising once the answer has been sent', async () => {
    // Reported: sending the same answer again recorded it a second time. The form stayed in
    // "submit" mode with everything still filled in, so a second press created a new response.
    const wrapper = mountComponent(PollView, { props: { pollToken: 'poll-token' } })
    await settle()

    const store = useAnsweringStore()
    store.displayName = 'Anna'
    store.setAnswer('day-1', 'yes')
    await wrapper.get('[data-testid="answer-form"]').trigger('submit')
    await settle()
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="answer-submit"]').text()).toContain(de.participate.save)
  })

  it('the second press revises instead of creating another response', async () => {
    // The point of the fix, stated as behaviour rather than as appearance.
    const wrapper = mountComponent(PollView, { props: { pollToken: 'poll-token' } })
    await settle()

    const store = useAnsweringStore()
    store.displayName = 'Anna'
    store.setAnswer('day-1', 'yes')

    await wrapper.get('[data-testid="answer-form"]').trigger('submit')
    await settle()
    await wrapper.vm.$nextTick()

    await wrapper.get('[data-testid="answer-form"]').trigger('submit')
    await settle()

    expect(vi.mocked(submitResponse)).toHaveBeenCalledTimes(1)
    expect(vi.mocked(reviseResponse)).toHaveBeenCalledTimes(1)
  })

  it('revises from the outset when opened through a personal link', async () => {
    const wrapper = mountComponent(PollView, { props: { editToken: 'tok-1' } })
    await settle()
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="answer-submit"]').text()).toContain(de.participate.save)
  })
})
