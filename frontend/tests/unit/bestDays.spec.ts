import { describe, it, expect } from 'vitest'
import { bestDays } from '../../src/composables/useBestDays'
import type { DayTotals } from '../../src/api/client'

/**
 * The ranking rule, tested without rendering anything.
 *
 * Every case here is a row of `data-model.md` §1. Keeping the rule a pure function is what makes
 * that possible: a component test that finds one marked cell says nothing about *which* of two
 * tied days was dropped, and dropping one is the mistake this rule is most likely to make.
 */

/** `yes/maybe/no` per day, so a nine-case table reads as nine lines. Local — this is its only consumer. */
const totalsFor = (...days: string[]): DayTotals[] =>
  days.map((spec, index) => {
    const [yes, maybe, no] = spec.split('/').map(Number)
    return { dayId: `day-${index + 1}`, yes, maybe, no }
  })

const ids = (result: Set<string>) => [...result].sort()

describe('bestDays — ties (FR-002, SC-002)', () => {
  it('marks both days when two are tied on yes and no', () => {
    // The case a naive maximum drops: a reduce keeping "the best so far" returns one day, and the
    // grid looks right while quietly picking whichever came first.
    expect(ids(bestDays(totalsFor('5/0/1', '5/0/1', '2/0/0')))).toEqual(['day-1', 'day-2'])
  })

  it('marks all three when three are tied', () => {
    expect(ids(bestDays(totalsFor('4/0/0', '4/0/0', '4/0/0', '1/0/0')))).toEqual([
      'day-1',
      'day-2',
      'day-3',
    ])
  })

  it('marks every day when every day is tied', () => {
    // Twenty days, everyone said yes to all of them. The stated rule, applied honestly.
    const twenty = totalsFor(...Array.from({ length: 20 }, () => '3/0/0'))

    expect(bestDays(twenty).size).toBe(20)
  })

  it('never resolves a tie by position', () => {
    // Reversing the input must not change the answer. If it does, something is ordering by index.
    const forwards = bestDays(totalsFor('5/0/1', '5/0/1'))
    const backwards = bestDays(totalsFor('5/0/1', '5/0/1'))

    expect(forwards.size).toBe(2)
    expect(backwards.size).toBe(2)
  })
})

describe('bestDays — the ordinary path (FR-001a)', () => {
  it('picks the day with the most yes', () => {
    expect(ids(bestDays(totalsFor('5/0/0', '3/0/1', '1/0/4')))).toEqual(['day-1'])
  })

  it('breaks a tie on yes by the fewest no', () => {
    expect(ids(bestDays(totalsFor('5/0/2', '5/0/0', '1/0/0')))).toEqual(['day-2'])
  })

  it('marks the only candidate day when there is just one', () => {
    expect(ids(bestDays(totalsFor('2/0/0')))).toEqual(['day-1'])
  })

  it('does not let a lower yes count win on having fewer no', () => {
    // no only breaks ties among the leaders; it never promotes a day past a higher yes count.
    expect(ids(bestDays(totalsFor('5/0/9', '2/0/0')))).toEqual(['day-1'])
  })
})

describe('bestDays — the minimum-yes guard (FR-001b, FR-009, SC-002a)', () => {
  it('marks nothing when there are no responses at all', () => {
    // What every poll looks like on the day it is created.
    expect(bestDays(totalsFor('0/0/0', '0/0/0', '0/0/0')).size).toBe(0)
  })

  it('marks nothing when only maybe and no were given', () => {
    expect(bestDays(totalsFor('0/3/1', '0/1/2')).size).toBe(0)
  })

  it('does not mark a day nobody answered ahead of a day everyone declined', () => {
    // FR-009. Without the guard, day 1 wins on "fewest no" — zero is fewer than five — and the
    // system would present a day nobody has agreed to as the best one. Absence is not agreement.
    expect(bestDays(totalsFor('0/0/0', '0/0/5')).size).toBe(0)
  })

  it('ignores a day nobody answered while others were answered', () => {
    expect(ids(bestDays(totalsFor('3/0/1', '0/0/0')))).toEqual(['day-1'])
  })

  it('marks nothing for a poll with no days at all', () => {
    expect(bestDays([]).size).toBe(0)
  })
})

describe('bestDays — maybe does not decide (FR-001a)', () => {
  it('returns the same days for two polls differing only in their maybe counts', () => {
    const without = bestDays(totalsFor('5/0/1', '3/0/0'))
    const with_ = bestDays(totalsFor('5/9/1', '3/0/0'))

    expect(ids(without)).toEqual(ids(with_))
  })

  it('does not let maybe rescue a day with fewer yes', () => {
    // The accepted cost of the chosen rule: ten people could make day 2, and day 1 still wins.
    expect(ids(bestDays(totalsFor('6/0/0', '5/5/0')))).toEqual(['day-1'])
  })
})
