# Phase 1 Data Model: Highlighting the Best Days

**Feature**: 006-highlight-best-days | **Date**: 2026-09-06

**Nothing is stored and nothing is migrated.** No entity gains a field, no table is added, and the
API contract is unchanged. That is worth stating rather than leaving to be noticed: a feature about
"the best day" sounds like one that records which day won, and recording it would be wrong — the
answer changes with every response, and a stored copy would be stale between the write that changed
it and the write that noticed.

What follows is one derived value and the exact rule that produces it.

---

## 1. Best days — derived, per render

**Input**: the day totals the results view already carries, one per candidate day:

```text
dayId    identifier
yes      count of participants who said yes
maybe    count who said maybe        (present, and deliberately unused)
no       count who said no
```

**Output**: the set of day ids that are best. Possibly empty. Never a single id — an empty set and a
set of one are different answers, and so are a set of one and a set of three.

### The rule (FR-001a, FR-001b, FR-002)

```text
candidates = days where yes > 0                    FR-001b: no yes, no candidacy
if candidates is empty          -> {}              nothing is marked
topYes     = max(yes of candidates)
leaders    = candidates where yes == topYes
fewestNo   = min(no of leaders)
result     = leaders where no == fewestNo          FR-002: ALL of them, never the first
```

**`maybe` appears nowhere in the rule.** It is shown in its own row and does not decide. That is the
accepted cost of the ranking chosen in clarification, and FR-007 exists because a reader will
otherwise read a marked day with fewer *maybe* answers as a defect.

### What the rule yields, case by case

Every row is a case from the specification's Edge Cases, and every row is one test (R-4).

| Case | Totals (yes/no) | Best | Why |
|---|---|---|---|
| A clear winner | 5/0, 3/1, 1/4 | day 1 | most yes |
| Tie on yes, broken by no | 5/2, 5/0, 1/0 | day 2 | same yes, fewer no |
| Tie on yes and no | 5/1, 5/1, 2/0 | days 1 **and** 2 | FR-002 — neither is picked over the other |
| Every day identical | 4/1, 4/1, 4/1 | all three | the stated rule, applied honestly |
| No responses at all | 0/0, 0/0 | none | FR-001b — no yes anywhere |
| Only maybe and no given | 0/3, 0/0 | none | FR-001b, and this is the case FR-009 turns on |
| One candidate day | 2/0 | day 1 | trivially |
| A day nobody answered | 3/1, 0/0 | day 1 | the unanswered day has no yes, so it never competes |
| Unanswered vs declined | 0/0, 0/5 | none | **both** fail FR-001b; without it the unanswered day would win on "fewest no" and FR-009 would be broken |

Rows three, four and five are **SC-002**; rows five and six are **SC-002a**.

The last row is the one that matters. It is the contradiction clarification found: with fewest-*no*
as the tie-break and no minimum on *yes*, a day nobody answered beats a day everyone declined.
FR-001b removes both from the running rather than special-casing the comparison.

---

## 2. What is NOT part of this

- **No ordering changes.** Days keep their chronological order (002 FR-013). The best day is
  emphasised where it already sits, not moved to the front.
- **No export field.** The export carries answers, not the system's reading of them (spec
  Assumptions). A consumer ranking the days for itself must not find a ranking already baked in.
- **No persistence, no cache, no invalidation.** The rule is a pass over at most 100 small records
  that are already in memory; anything remembered would need a reason to be recomputed. This is also
  what makes **FR-006** true without machinery: the mark is derived from the totals of the response
  that has just been fetched, so a new answer, a revised one or one the creator deleted moves the
  mark by arriving, not by invalidating anything.
