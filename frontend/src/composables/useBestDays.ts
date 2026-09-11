import { computed, type Ref } from 'vue'
import type { DayTotals } from '../api/client'

/**
 * Which candidate days are the best ones (FR-001a, FR-001b, FR-002).
 *
 * **The rule, in full**: a day is best when it has at least one *yes* and no other day has more.
 * Where several share the highest *yes* count, the ones among them with the fewest *no* are best.
 * *Maybe* does not enter the comparison — it is shown in its own row and does not decide.
 *
 * Two parts of that are easy to get wrong and are the reason this is a function rather than an
 * expression inside a template:
 *
 * - **Every tied day is returned, never the first of them.** A `reduce` keeping "the best so far"
 *   returns one day and the grid looks correct while quietly picking whichever came first. That is
 *   the mistake FR-002 exists to forbid, and it is invisible in a rendered test that only counts
 *   marks.
 * - **`yes > 0` is the first clause, not an afterthought.** With fewest-*no* as the tie-break, a day
 *   nobody answered (0 yes, 0 no) would beat a day everyone declined (0 yes, 5 no), because zero is
 *   fewer than five - and the system would present a day nobody has agreed to as the best one.
 *   FR-009 depends entirely on this clause.
 *
 * Returns an empty set when no day has a *yes*, which is what every poll looks like on the day it
 * is created.
 */
export function bestDays(totals: readonly DayTotals[]): Set<string> {
  const candidates = totals.filter((day) => day.yes > 0)

  if (candidates.length === 0) {
    return new Set()
  }

  const topYes = Math.max(...candidates.map((day) => day.yes))
  const leaders = candidates.filter((day) => day.yes === topYes)

  const fewestNo = Math.min(...leaders.map((day) => day.no))

  return new Set(leaders.filter((day) => day.no === fewestNo).map((day) => day.dayId))
}

/**
 * The same rule for a component to consume.
 *
 * Recomputed from whatever totals are currently in hand, which is what makes FR-006 true without
 * any machinery: a new response, a revised one or one the creator deleted moves the mark by
 * arriving, because the totals it is derived from arrive with it. Nothing is remembered, so nothing
 * has to be invalidated.
 */
export function useBestDays(totals: Ref<readonly DayTotals[]>) {
  return computed(() => bestDays(totals.value))
}
