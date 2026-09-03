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

  // --- The per-day summary (004) ------------------------------------------------------------
  //
  // These grew out of one test that read the totals at the foot of the grid. The numbers it
  // checked have not changed; where and when they appear has (004 FR-021).

  const unfold = async (wrapper: ReturnType<typeof mountGrid>) => {
    await wrapper.get('[data-testid="summary-toggle"]').trigger('click')
    return wrapper
  }

  const summaryRows = (wrapper: ReturnType<typeof mountGrid>) =>
    wrapper.findAll('[data-testid="summary-row"]')

  it('shows no counts at all until asked', async () => {
    // 004 FR-003, SC-001. The whole point of the move: the dates are the first thing under the
    // heading, and nothing has to be scrolled past to reach them.
    const wrapper = mountGrid()

    expect(summaryRows(wrapper)).toHaveLength(0)
    expect(wrapper.find('[data-testid="totals-row"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="summary-toggle"]').exists()).toBe(true)
  })

  it('shows the per-day totals for the three answered states once unfolded', async () => {
    // 002 FR-033, 004 FR-001: three rows, and they need not sum to the response count.
    const wrapper = await unfold(mountGrid())

    const rows = summaryRows(wrapper)
    expect(rows.map((r) => r.attributes('data-state'))).toEqual(['yes', 'maybe', 'no'])
    expect(rows[0].findAll('td')[0].text()).toBe('2')
    expect(rows[2].findAll('td')[1].text()).toBe('1')
  })

  it('puts every summary row above the row carrying the dates', async () => {
    // 004 FR-001 and SC-002. This is what "above the date" means in a form a test can read:
    // position in the document, not a class name that hints at position.
    const wrapper = await unfold(mountGrid())

    const rows = [...wrapper.get('thead').element.querySelectorAll('tr')]
    const dateRow = rows.findIndex((r) => r.textContent?.includes(de.results.participant))
    const summaries = rows
      .map((r, i) => (r.getAttribute('data-testid') === 'summary-row' ? i : -1))
      .filter((i) => i >= 0)

    expect(summaries).toHaveLength(3)
    expect(dateRow).toBeGreaterThan(-1)
    for (const index of summaries) expect(index).toBeLessThan(dateRow)
  })

  it('keeps no tally below the responses', async () => {
    // 004 FR-002 and SC-003. The old identifier is deliberately not reused for the new rows: a
    // test nobody updated would otherwise pass against something it was not written for.
    const wrapper = await unfold(mountGrid())

    expect(wrapper.find('tfoot').exists()).toBe(false)
    expect(wrapper.find('[data-testid="totals-row"]').exists()).toBe(false)
  })

  it('lines each summary label up with the column that names the participants', async () => {
    // 004 FR-001a. Three numbers stacked over a date are unreadable unless the label to their
    // left says which is which - and it only says it if it is in the same column.
    const wrapper = await unfold(mountGrid())

    const nameHeader = wrapper
      .get('thead')
      .element.querySelector('tr:not([data-testid="summary-row"]) th')
    expect(nameHeader?.textContent).toContain(de.results.participant)

    for (const row of summaryRows(wrapper)) {
      const first = row.element.firstElementChild
      expect(first?.tagName).toBe('TH')
      expect(first?.textContent?.trim().length).toBeGreaterThan(0)
      // Same column index as the name header: both are the first cell of their row.
      expect([...row.element.children].indexOf(first!)).toBe(0)
    }
  })

  it('carries a mark beside each summary label, not colour alone', async () => {
    // 004 FR-010, inherited from 002 FR-053.
    const wrapper = await unfold(mountGrid())

    const icons = summaryRows(wrapper).map((r) =>
      r.find('.v-icon').classes().filter((c) => c.startsWith('mdi-')).join(' '),
    )
    expect(icons.filter(Boolean)).toHaveLength(3)
    expect(new Set(icons).size).toBe(3)
  })

  it('shows zero for a day nobody answered, and lets the three numbers not add up', async () => {
    // 004 FR-009 and FR-011. An empty cell would read as "not asked"; the second day here was
    // asked and declined once, and its yes and maybe are genuinely zero. Across the poll the
    // three counts total four while three people answered - which 002 FR-033 permits, because
    // a day left blank is counted in none of them.
    const wrapper = await unfold(mountGrid())
    const rows = summaryRows(wrapper)

    expect(rows[0].findAll('td')[1].text()).toBe('0')
    expect(rows[1].findAll('td')[1].text()).toBe('0')
    expect(rows[2].findAll('td')[0].text()).toBe('0')
  })

  it('offers neither counts nor a control when nobody has answered', async () => {
    // 004 FR-012: a control that unfolds to zeros in every column is furniture.
    const wrapper = mountGrid({ ...POLL, responses: [], responseCount: 0 })

    expect(wrapper.find('[data-testid="summary-toggle"]').exists()).toBe(false)
    expect(summaryRows(wrapper)).toHaveLength(0)
  })

  it('announces whether it is folded, and forgets the state on the next visit', async () => {
    // 004 FR-005, FR-007, FR-008. The last part matters most: remembering how this reader last
    // had it would mean storing something about a reader who is deliberately anonymous.
    const wrapper = mountGrid()
    const toggle = () => wrapper.get('[data-testid="summary-toggle"]')

    expect(toggle().attributes('aria-expanded')).toBe('false')
    await toggle().trigger('click')
    expect(toggle().attributes('aria-expanded')).toBe('true')
    await toggle().trigger('click')
    expect(toggle().attributes('aria-expanded')).toBe('false')

    // A second, independent mount - the next visit.
    expect(mountGrid().get('[data-testid="summary-toggle"]').attributes('aria-expanded')).toBe(
      'false',
    )
  })

  it('unfolds a poll at the declared limits well inside a second', async () => {
    // 004 SC-005. A hundred days, and the one page of fifty responses the grid ever holds -
    // so this is 3 x 101 cells, not 100,000. Handed in as props: a real poll of that size costs
    // minutes to seed and would prove nothing more about the unfolding itself.
    const days = Array.from({ length: 100 }, (_, i) => ({
      id: `day-${i}`,
      date: `2027-01-${String((i % 28) + 1).padStart(2, '0')}`,
    }))
    const large = {
      ...POLL,
      days,
      totals: days.map((d, i) => ({ dayId: d.id, yes: i % 7, maybe: i % 3, no: i % 5 })),
      responses: Array.from({ length: 50 }, (_, i) => ({
        id: `r${i}`,
        displayName: `Person ${i}`,
        answers: days.slice(0, 50).map((d) => ({ dayId: d.id, availability: 'yes' as const })),
      })),
      responseCount: 1000,
      pageCount: 20,
    }

    const wrapper = mountGrid(large)
    const started = performance.now()
    await wrapper.get('[data-testid="summary-toggle"]').trigger('click')
    const elapsed = performance.now() - started

    expect(summaryRows(wrapper)).toHaveLength(3)
    expect(summaryRows(wrapper)[0].findAll('td')).toHaveLength(100)
    expect(elapsed).toBeLessThan(1000)
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
