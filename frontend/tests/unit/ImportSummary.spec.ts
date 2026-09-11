import { describe, it, expect } from 'vitest'
import { mountComponent, de } from '../support/mount'
import ImportSummary from '../../src/components/admin/ImportSummary.vue'
import type { ImportSummary as Summary } from '../../src/api/client'

/**
 * The summary is the whole visible outcome of an import, and two of its states are easy to get
 * wrong in ways that mislead an operator:
 *
 *  - "nothing taken" is a *specified* outcome (FR-004), not a failure. A file holding one expired
 *    poll produces exactly that, and it must not be dressed as an error.
 *  - the skipped list must be absent when empty. An empty "Nicht übernommen" heading reads as a
 *    defect that the operator then goes looking for.
 */
const summary = (over: Partial<Summary> = {}): Summary => ({
  imported: true,
  pollId: '0199aaaa-0000-7000-8000-000000000001',
  participantToken: 'abcdefghijklmnopqrstuv',
  counts: { days: 3, responses: 2, answers: 5 },
  skipped: [],
  ...over,
})

const mount = (over: Partial<Summary> = {}) =>
  mountComponent(ImportSummary, { props: { summary: summary(over) } })

describe('ImportSummary (005 FR-003, FR-004, FR-007, SC-003, SC-009)', () => {
  it('reports what was taken', () => {
    const text = mount().text()

    expect(text).toContain('3')
    expect(text).toContain('2')
  })

  it('shows the new participant link and says the old ones do not reach it', () => {
    // FR-007 and SC-009: the operator must be able to see that link continuity did not happen.
    const wrapper = mount()

    expect(wrapper.find('[data-testid="share-link"]').exists()).toBe(true)
    expect(wrapper.text()).toContain(de.import.newLinkHint)
  })

  it('hides the skipped list entirely when nothing was skipped', () => {
    const wrapper = mount()

    expect(wrapper.find('[data-testid="import-skipped"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain(de.import.skippedTitle)
  })

  it('renders one row per skipped item, from its reason code', () => {
    const wrapper = mount({
      skipped: [
        { kind: 'answer', reason: 'unknown_day', detail: '2027-06-11' },
        { kind: 'response', reason: 'name_too_long', detail: '100' },
      ],
    })

    const rows = wrapper.findAll('[data-testid="import-skipped"] li')

    expect(rows).toHaveLength(2)
    expect(rows[0].text()).toContain('2027-06-11')
  })

  it('never renders a raw reason code at the operator', () => {
    // The codes are machine-readable; the interface owns the words (002 FR-029).
    const wrapper = mount({
      skipped: [{ kind: 'poll', reason: 'already_expired', detail: null }],
    })

    expect(wrapper.text()).not.toContain('already_expired')
    expect(wrapper.text()).toContain(de.import.skip.already_expired)
  })

  it('says plainly that nothing was taken, without the link block', () => {
    // FR-004. Neutral register, not an error, and no link because there is no poll.
    const wrapper = mount({
      imported: false,
      pollId: null,
      participantToken: null,
      counts: { days: 0, responses: 0, answers: 0 },
      skipped: [{ kind: 'poll', reason: 'already_expired', detail: null }],
    })

    expect(wrapper.text()).toContain(de.import.nothingTaken)
    expect(wrapper.find('[data-testid="share-link"]').exists()).toBe(false)
  })

  it('offers no way to reopen a past summary, because none is kept', () => {
    // FR-003a: one request, no history. An interface that implied otherwise would be promising
    // something the system deliberately does not store.
    const text = mount().text().toLowerCase()

    expect(text).not.toContain('verlauf')
    expect(text).not.toContain('historie')
  })
})
