import { describe, it, expect } from 'vitest'
import { mountComponent, de } from '../support/mount'
import ResultGrid from '../../src/components/poll/ResultGrid.vue'

const POLL = {
  title: 'Grillabend',
  message: null,
  days: [
    { id: 'day-1', date: '2026-11-18' },
    { id: 'day-2', date: '2026-11-20' },
  ],
  totals: [
    { dayId: 'day-1', yes: 2, maybe: 1, no: 0 },
    { dayId: 'day-2', yes: 0, maybe: 0, no: 1 },
  ],
  responses: [
    { id: 'r1', displayName: 'Anna', answers: [{ dayId: 'day-1', availability: 'yes' as const }] },
    {
      id: 'r2',
      displayName: 'Bernd',
      answers: [
        { dayId: 'day-1', availability: 'maybe' as const },
        { dayId: 'day-2', availability: 'no' as const },
      ],
    },
  ],
  page: 1,
  pageCount: 1,
  responseCount: 2,
}

const mountGrid = (poll = POLL, deletable = false) =>
  mountComponent(ResultGrid, { props: { poll, deletable } })

describe('ResultGrid', () => {
  it('renders one row per response', () => {
    expect(mountGrid().findAll('[data-testid="result-row"]')).toHaveLength(2)
  })

  it('marks an unanswered day distinctly from a rejected one', () => {
    // FR-024a: the empty cell must never be mistaken for "no time".
    //
    // This asserts on what a sighted person actually sees - the icon. An earlier version
    // compared data-state and the screen-reader text, both of which stay different even when
    // the two cells render identically, so rendering "no answer" exactly like "no" passed.
    const wrapper = mountGrid()
    const cells = wrapper.findAll('[data-testid="result-cell"]')

    // Anna answered day-1 only, so her day-2 cell is the unanswered one.
    const unanswered = cells[1]
    const rejected = cells[3]

    expect(unanswered.attributes('data-state')).toBe('none')
    expect(rejected.attributes('data-state')).toBe('no')

    const iconOf = (cell: (typeof cells)[number]) =>
      cell.find('.v-icon').classes().filter((c) => c.startsWith('mdi-')).join(' ')

    expect(iconOf(unanswered)).not.toBe('')
    expect(iconOf(unanswered)).not.toBe(iconOf(rejected))
    expect(unanswered.find('.d-sr-only').text()).not.toBe(rejected.find('.d-sr-only').text())
  })

  it('gives all four states their own visible mark', () => {
    // Three answered states plus the absence of an answer. If any two share an icon, the grid
    // has become ambiguous for anyone reading it rather than inspecting the DOM.
    const wrapper = mountGrid()

    const icons = wrapper.findAll('[data-testid="result-cell"]').map((cell) => ({
      state: cell.attributes('data-state'),
      icon: cell.find('.v-icon').classes().filter((c) => c.startsWith('mdi-')).join(' '),
    }))

    const byState = new Map(icons.map((i) => [i.state, i.icon]))
    expect(byState.size).toBe(4)
    expect(new Set(byState.values()).size).toBe(4)
  })

  it('conveys every state by a character, not by colour alone', () => {
    // FR-053 and SC-026: the grid must survive greyscale.
    const wrapper = mountGrid()
    const cells = wrapper.findAll('[data-testid="result-cell"]')

    // Each state carries a distinct icon *and* a distinct screen-reader label, so it survives
    // greyscale and survives having no colour perception at all. Counted per state rather than
    // per cell: four cells sharing two icons would otherwise look like "enough variety".
    const perState = new Map(
      cells.map((c) => [
        c.attributes('data-state'),
        {
          icon: c.find('.v-icon').classes().filter((k) => k.startsWith('mdi-')).join(' '),
          label: c.find('.d-sr-only').text(),
        },
      ]),
    )

    expect(perState.size).toBe(4)
    expect(new Set([...perState.values()].map((v) => v.icon)).size).toBe(4)
    expect(new Set([...perState.values()].map((v) => v.label)).size).toBe(4)
  })

  it('gives every cell a screen-reader label as well as a symbol', () => {
    const wrapper = mountGrid()

    for (const cell of wrapper.findAll('[data-testid="result-cell"]')) {
      expect(cell.find('.d-sr-only').exists()).toBe(true)
      expect(cell.find('.d-sr-only').text().length).toBeGreaterThan(0)
    }
  })

  it('shows the per-day totals for the three answered states', () => {
    // FR-033: three rows, and they need not sum to the response count.
    const wrapper = mountGrid()

    const rows = wrapper.findAll('[data-testid="totals-row"]')
    expect(rows.map((r) => r.attributes('data-state'))).toEqual(['yes', 'maybe', 'no'])
    expect(rows[0].findAll('td')[0].text()).toBe('2')
    expect(rows[2].findAll('td')[1].text()).toBe('1')
  })

  it('shows the response count so the totals are interpretable', () => {
    // FR-033a: without it, "2 yes" could be 2 of 2 or 2 of 40.
    expect(mountGrid().get('[data-testid="response-count"]').text()).toContain('2')
  })

  it('shows an explicit empty state rather than a bare table', () => {
    // FR-034
    const empty = { ...POLL, responses: [], responseCount: 0 }
    const wrapper = mountGrid(empty)

    expect(wrapper.get('[data-testid="results-empty"]').text()).toContain(de.results.empty)
    expect(wrapper.find('table').exists()).toBe(false)
  })

  it('offers per-response deletion only when asked to', () => {
    // FR-037a is an operator capability, not a participant one.
    expect(mountGrid(POLL, false).find('[data-testid="delete-response"]').exists()).toBe(false)
    expect(mountGrid(POLL, true).find('[data-testid="delete-response"]').exists()).toBe(true)
  })
})
