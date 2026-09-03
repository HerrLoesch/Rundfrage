import { describe, it, expect, vi, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mountComponent, de } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  fetchPoll: vi.fn(),
  submitResponse: vi.fn(),
  fetchOwnResponse: vi.fn(),
  reviseResponse: vi.fn(),
}))

import AnswerForm from '../../src/components/poll/AnswerForm.vue'
import { useAnsweringStore } from '../../src/stores/answering'

const POLL = {
  title: 'Grillabend',
  message: null,
  days: [
    { id: 'day-1', date: '2026-11-18' },
    { id: 'day-2', date: '2026-11-20' },
  ],
  totals: [],
  responses: [],
  page: 1,
  pageCount: 1,
  responseCount: 0,
}

function mountForm(mode: 'submit' | 'revise' = 'submit') {
  let store!: ReturnType<typeof useAnsweringStore>

  // The poll must be in the store before the first render, or the day list is empty and every
  // assertion about it passes against nothing.
  const wrapper = mountComponent(AnswerForm, { props: { mode } }, () => {
    store = useAnsweringStore()
    store.poll = POLL as never
  })

  return { wrapper, store }
}

describe('AnswerForm', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('offers exactly three native radios per candidate day', () => {
    // FR-023 and research.md R-11: native controls, so keyboard operation and labelling are
    // the platform's job rather than ours.
    const { wrapper } = mountForm()

    const days = wrapper.findAll('[data-testid="day-choice"]')
    expect(days).toHaveLength(2)

    for (const day of days) {
      // Vuetify's v-radio renders a real <input type="radio">, which is why the accessibility
      // argument of research.md R-11 still holds after the visual rewrite.
      const radios = day.findAll('input[type="radio"]')
      expect(radios).toHaveLength(3)
      // One group per day, so choosing "yes" for Wednesday cannot unset Friday.
      const names = new Set(radios.map((r) => r.attributes('name')))
      expect(names.size).toBe(1)
    }
  })

  it('starts with no day answered', () => {
    // FR-024: an unanswered day is the default and stores nothing.
    const { wrapper } = mountForm()

    expect(wrapper.findAll('input[type="radio"]:checked')).toHaveLength(0)
  })

  it('records a choice per day independently', async () => {
    const { wrapper, store } = mountForm()

    const days = wrapper.findAll('[data-testid="day-choice"]')
    await days[0].get('[data-testid="choice-yes"]').find('input').setValue()
    await days[1].get('[data-testid="choice-no"]').find('input').setValue()

    expect(store.answers).toEqual({ 'day-1': 'yes', 'day-2': 'no' })
  })

  it('shows the visibility notice before the name field', () => {
    // FR-036a: nobody should discover after submitting that their name is public.
    const { wrapper } = mountForm()

    const html = wrapper.html()
    const noticeAt = html.indexOf('data-testid="visibility-notice"')
    const nameAt = html.indexOf('data-testid="participant-name"')

    expect(noticeAt).toBeGreaterThan(-1)
    expect(nameAt).toBeGreaterThan(-1)
    expect(noticeAt).toBeLessThan(nameAt)
    expect(wrapper.get('[data-testid="visibility-notice"]').text()).toBe(
      de.participate.visibilityNotice,
    )
  })

  it('every radio carries a visible label', () => {
    // FR-051: an icon or a colour is not a label.
    const { wrapper } = mountForm()

    for (const choice of wrapper.findAll('[data-testid^="choice-"]')) {
      expect(choice.text().trim().length).toBeGreaterThan(0)
      expect(choice.find('input[type="radio"]').exists()).toBe(true)
    }
  })

  it('names the button differently when revising', () => {
    expect(mountForm('submit').wrapper.get('[data-testid="answer-submit"]').text())
      .toContain(de.participate.submit)
    setActivePinia(createPinia())
    expect(mountForm('revise').wrapper.get('[data-testid="answer-submit"]').text())
      .toContain(de.participate.save)
  })
})

describe('AnswerForm submit wiring', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('emits submit exactly once per click', async () => {
    // Without an explicit defineEmits, Vue also binds the parent's @submit as a native listener
    // on the root <form> - the handler ran twice and every answer was submitted twice.
    const { wrapper } = mountForm()

    await wrapper.get('[data-testid="answer-form"]').trigger('submit')

    expect(wrapper.emitted('submit')).toHaveLength(1)
  })

  it('declares submit as a component event so it does not fall through', () => {
    const { wrapper } = mountForm()

    // A declared emit is removed from the fallthrough attributes; an undeclared one is not.
    expect(wrapper.vm.$options.emits).toContain('submit')
  })
})

