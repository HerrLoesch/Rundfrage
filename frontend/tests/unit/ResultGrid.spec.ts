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
    // find, not get: get() throws when the element is missing, so exists() on its result can
    // only ever be true. The assertion read as a check and was a constant.
    expect(wrapper.find('[data-testid="summary-toggle"]').exists()).toBe(true)
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

    // 006: the marks survive the documented maximum, and land only on days holding the highest
    // yes count. `yes: i % 7` peaks at six, so several days tie there and every one of them is
    // marked (FR-002) - the pathological tie research R-3 accepted, exercised rather than assumed.
    const yesCells = summaryRows(wrapper)[0].findAll('td')
    const markedDays = yesCells
      .map((cell, index) => (cell.find('[data-testid="best-day"]').exists() ? index : -1))
      .filter((index) => index >= 0)

    expect(markedDays.length).toBeGreaterThan(0)
    expect(markedDays.every((index) => large.totals[index].yes === 6)).toBe(true)
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

/**
 * 006: the best day is marked in the summary.
 *
 * The rule itself is proven in `bestDays.spec.ts` against the nine cases of data-model.md §1.
 * What is checked here is everything the rule cannot see: where the mark sits, when it exists,
 * what it says, and that it changed nothing else.
 */
describe('ResultGrid — the best day (006)', () => {
  const unfold = async (wrapper: ReturnType<typeof mountGrid>) => {
    await wrapper.get('[data-testid="summary-toggle"]').trigger('click')
    return wrapper
  }

  const marks = (wrapper: ReturnType<typeof mountGrid>) =>
    wrapper.findAll('[data-testid="best-day"]')

  /** The cells of one summary row, in day order. */
  const cellsOf = (wrapper: ReturnType<typeof mountGrid>, state: string) =>
    wrapper.get(`[data-testid="summary-row"][data-state="${state}"]`).findAll('td')

  const pollWhere = (...totals: Array<[string, number, number, number]>) => ({
    ...POLL,
    days: totals.map(([id]) => ({ id, date: '2027-05-01' })),
    totals: totals.map(([dayId, yes, maybe, no]) => ({ dayId, yes, maybe, no })),
  })

  it('marks the day that leads, and only that day', async () => {
    // FR-001. The default fixture has day-1 at two yes and day-2 at none.
    const wrapper = await unfold(mountGrid())

    expect(marks(wrapper)).toHaveLength(1)
    expect(cellsOf(wrapper, 'yes')[0].find('[data-testid="best-day"]').exists()).toBe(true)
    expect(cellsOf(wrapper, 'yes')[1].find('[data-testid="best-day"]').exists()).toBe(false)
  })

  it('marks the yes row only, never the maybe or no cells of the same day', async () => {
    // research R-2: the yes count is what decided, so it is what carries the mark. Marking all
    // three would suggest all three numbers are being praised.
    const wrapper = await unfold(mountGrid())

    expect(cellsOf(wrapper, 'maybe').some((c) => c.find('[data-testid="best-day"]').exists())).toBe(
      false,
    )
    expect(cellsOf(wrapper, 'no').some((c) => c.find('[data-testid="best-day"]').exists())).toBe(
      false,
    )
  })

  it('shows no mark anywhere while the summary is folded', async () => {
    // FR-008a: a folded grid looks exactly as it did before this feature.
    expect(marks(mountGrid())).toHaveLength(0)
  })

  it('shows the same mark again after folding and unfolding', async () => {
    // Not computed once and remembered: a mark cached on the first unfolding would pass every
    // other test here and be wrong the moment anything changed underneath it.
    const wrapper = await unfold(mountGrid())
    const first = cellsOf(wrapper, 'yes').findIndex((c) =>
      c.find('[data-testid="best-day"]').exists(),
    )

    await wrapper.get('[data-testid="summary-toggle"]').trigger('click')
    expect(marks(wrapper)).toHaveLength(0)

    await wrapper.get('[data-testid="summary-toggle"]').trigger('click')
    const second = cellsOf(wrapper, 'yes').findIndex((c) =>
      c.find('[data-testid="best-day"]').exists(),
    )

    expect(second).toBe(first)
    expect(marks(wrapper)).toHaveLength(1)
  })

  it('names itself „bester Tag" without swallowing the rule into that name', async () => {
    // ui-contract §2. The rule is a description, not part of the name - 004 FR-016a learned what
    // happens when explanatory text is glued into an element's own text.
    const mark = (await unfold(mountGrid())).get('[data-testid="best-day"]')

    expect(mark.attributes('aria-label')).toBe(de.results.bestDay)
    expect(mark.attributes('aria-label')).not.toContain(de.results.bestDayRule)
    expect(mark.attributes('aria-describedby')).toBeTruthy()
  })

  it('carries the rule where hover and keyboard focus both reach it', async () => {
    // FR-007, SC-004a, research R-3: a title alone is unreachable without a pointer, so the mark
    // is focusable.
    const wrapper = await unfold(mountGrid())
    const mark = wrapper.get('[data-testid="best-day"]')

    expect(mark.attributes('title')).toBe(de.results.bestDayRule)
    expect(mark.attributes('tabindex')).toBe('0')

    const described = wrapper.find(`#${mark.attributes('aria-describedby')}`)
    expect(described.exists()).toBe(true)
    expect(described.text()).toBe(de.results.bestDayRule)
  })

  it('is distinguishable without colour', async () => {
    // FR-005, SC-004: a colour class alone excludes a reader who cannot see the difference, so the
    // mark has to render a glyph. Asserted on the icon class because that is the only observable
    // proxy for "a shape is drawn" in a DOM without a renderer - the mark IS the icon, so a search
    // for one inside it finds nothing.
    const mark = (await unfold(mountGrid())).get('[data-testid="best-day"]')

    expect(mark.classes()).toContain('v-icon')
    expect(mark.classes().some((c) => c.startsWith('mdi-'))).toBe(true)
  })

  it('reflects the whole poll rather than the responses on screen', async () => {
    // FR-003, SC-005. The totals say day-1 leads; the responses visible on this page all favour
    // day-2. A mark derived from the rows would follow the page and move as the reader pages.
    const wrapper = await unfold(
      mountGrid({
        ...pollWhere(['day-1', 5, 0, 0], ['day-2', 1, 0, 0]),
        page: 2,
        pageCount: 4,
        responseCount: 200,
        responses: [
          { id: 'r9', displayName: 'Zoe', answers: [{ dayId: 'day-2', availability: 'yes' }] },
        ],
      }),
    )

    expect(cellsOf(wrapper, 'yes')[0].find('[data-testid="best-day"]').exists()).toBe(true)
    expect(cellsOf(wrapper, 'yes')[1].find('[data-testid="best-day"]').exists()).toBe(false)
  })

  it('leaves every count, every day and their order exactly as they were', async () => {
    // FR-008, SC-006: the mark adds emphasis and nothing else.
    const wrapper = await unfold(mountGrid())

    expect(cellsOf(wrapper, 'yes').map((c) => c.text().replace(/\D/g, ''))).toEqual(['2', '0'])
    expect(cellsOf(wrapper, 'maybe').map((c) => c.text())).toEqual(['1', '0'])
    expect(cellsOf(wrapper, 'no').map((c) => c.text())).toEqual(['0', '1'])
    expect(wrapper.findAll('[data-testid="result-row"]')).toHaveLength(2)
  })

  it('moves when the answers change', async () => {
    // FR-006. Nothing is remembered, so nothing has to be invalidated: the mark follows the totals
    // that arrive with the next response.
    const wrapper = await unfold(mountGrid(pollWhere(['day-1', 3, 0, 0], ['day-2', 1, 0, 0])))
    expect(cellsOf(wrapper, 'yes')[0].find('[data-testid="best-day"]').exists()).toBe(true)

    await wrapper.setProps({ poll: pollWhere(['day-1', 3, 0, 0], ['day-2', 9, 0, 0]) })

    expect(cellsOf(wrapper, 'yes')[0].find('[data-testid="best-day"]').exists()).toBe(false)
    expect(cellsOf(wrapper, 'yes')[1].find('[data-testid="best-day"]').exists()).toBe(true)
  })
})

/**
 * 006 US2: ties, and the honest emptiness.
 *
 * The rule already returns a set, so these may well pass the moment US1 lands. That is the intended
 * outcome, not a gap — they are the proof at the surface where an operator would see it fail, and
 * the place a later "just take the first one" refactor would be caught.
 */
describe('ResultGrid — no single best day (006 US2)', () => {
  const unfold = async (wrapper: ReturnType<typeof mountGrid>) => {
    await wrapper.get('[data-testid="summary-toggle"]').trigger('click')
    return wrapper
  }

  const pollWhere = (...totals: Array<[number, number, number]>) => ({
    ...POLL,
    days: totals.map((_, i) => ({ id: `day-${i + 1}`, date: '2027-05-01' })),
    totals: totals.map(([yes, maybe, no], i) => ({ dayId: `day-${i + 1}`, yes, maybe, no })),
  })

  const markedIndexes = (wrapper: ReturnType<typeof mountGrid>) =>
    wrapper
      .get('[data-testid="summary-row"][data-state="yes"]')
      .findAll('td')
      .map((cell, index) => (cell.find('[data-testid="best-day"]').exists() ? index : -1))
      .filter((index) => index >= 0)

  it('marks both days when two are tied at the top', async () => {
    // FR-002: neither is picked over the other, because nothing in the answers picks one.
    const wrapper = await unfold(mountGrid(pollWhere([4, 0, 1], [4, 0, 1], [2, 0, 0])))

    expect(markedIndexes(wrapper)).toEqual([0, 1])
  })

  it('marks exactly the three tied days among ten', async () => {
    const tied: Array<[number, number, number]> = Array.from({ length: 10 }, (_, i) =>
      i < 3 ? [7, 0, 0] : [2, 0, 0],
    )

    expect(markedIndexes(await unfold(mountGrid(pollWhere(...tied))))).toEqual([0, 1, 2])
  })

  it('shows no mark and no explanation when nobody has answered', async () => {
    // FR-001b, SC-002a. This is what every poll looks like on the day it is created, so a line
    // saying "no day leads yet" would appear on all of them - noise, not information.
    const wrapper = await unfold(mountGrid(pollWhere([0, 0, 0], [0, 0, 0])))

    expect(markedIndexes(wrapper)).toEqual([])
    expect(wrapper.text()).not.toContain(de.results.bestDay)
  })

  it('shows no mark when only maybe and no were given', async () => {
    // Whatever the no counts say: without a yes there is no best day (FR-001b).
    const wrapper = await unfold(mountGrid(pollWhere([0, 4, 0], [0, 1, 3])))

    expect(markedIndexes(wrapper)).toEqual([])
  })

  it('marks every tied day identically — no primary and secondary', async () => {
    const wrapper = await unfold(mountGrid(pollWhere([4, 0, 1], [4, 0, 1], [4, 0, 1])))
    const marks = wrapper.findAll('[data-testid="best-day"]')

    expect(marks).toHaveLength(3)

    const shapes = marks.map((m) => m.classes().filter((c) => c.startsWith('mdi-')).join())
    const labels = marks.map((m) => m.attributes('aria-label'))

    expect(new Set(shapes).size).toBe(1)
    expect(new Set(labels).size).toBe(1)
  })

  /**
   * The paging control, which the API and the catalogue have been waiting for since 002.
   *
   * `ResultsProjection` pages server-side at fifty and the payload has carried `page`/`pageCount`
   * all along; 002 research R-7 chose paging over a virtual list, and 004's UI contract lists
   * "the paging control and its page size" among the things it leaves unchanged. Nothing ever
   * rendered it, so a poll at 002's limit of 1000 responses showed fifty of them and offered no
   * way to the other 950. Found by review of feature 007, which gave the grid an address of its
   * own and made the gap plain.
   */
  describe('paging', () => {
    const paged = (page: number, pageCount: number) => ({
      ...POLL,
      page,
      pageCount,
      responseCount: pageCount * 50,
    })

    it('offers no paging control when everything fits on one page', () => {
      // The common case by far. A control that always says "1 of 1" is noise.
      expect(mountGrid().find('[data-testid="results-paging"]').exists()).toBe(false)
    })

    it('says which page is shown, and of how many', () => {
      const wrapper = mountGrid(paged(2, 5))

      expect(wrapper.get('[data-testid="results-paging"]').text())
        .toContain(de.results.page.replace('{page}', '2').replace('{pageCount}', '5'))
    })

    it('asks for the next page rather than changing anything itself', () => {
      // The rows live on the server, so the grid reports the intent and the view that owns the
      // request acts on it - the same split the delete control already uses.
      const wrapper = mountGrid(paged(2, 5))

      wrapper.get('[data-testid="results-next"]').trigger('click')

      expect(wrapper.emitted('changePage')).toEqual([[3]])
    })

    it('asks for the previous page', () => {
      const wrapper = mountGrid(paged(2, 5))

      wrapper.get('[data-testid="results-previous"]').trigger('click')

      expect(wrapper.emitted('changePage')).toEqual([[1]])
    })

    it('cannot be asked to go before the first page', () => {
      const wrapper = mountGrid(paged(1, 5))

      expect(wrapper.get('[data-testid="results-previous"]').attributes('disabled')).toBeDefined()
      expect(wrapper.get('[data-testid="results-next"]').attributes('disabled')).toBeUndefined()
    })

    it('cannot be asked to go past the last page', () => {
      const wrapper = mountGrid(paged(5, 5))

      expect(wrapper.get('[data-testid="results-next"]').attributes('disabled')).toBeDefined()
      expect(wrapper.get('[data-testid="results-previous"]').attributes('disabled')).toBeUndefined()
    })

    it('names both controls for a screen reader', () => {
      const wrapper = mountGrid(paged(2, 5))

      expect(wrapper.get('[data-testid="results-previous"]').text()).toContain(de.results.previous)
      expect(wrapper.get('[data-testid="results-next"]').text()).toContain(de.results.next)
    })
  })
})
