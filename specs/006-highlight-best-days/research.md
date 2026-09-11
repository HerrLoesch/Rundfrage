# Phase 0 Research: Highlighting the Best Days

**Feature**: 006-highlight-best-days | **Date**: 2026-09-06

Four questions. The first removes half the feature before it is written; the third is the only one
with a genuine tension in it.

---

## R-1: The mark needs no backend change, and FR-003 holds for free

**Decision**: derive the best days on the client from the `totals` the results view already carries.
Nothing is added to the API, nothing is computed twice, and no new field is stored.

**Checked, not assumed.** FR-003 requires the mark to reflect every response rather than the page on
screen, and a results view that paged its counts would break it silently — the marked day would move
as the reader pages. `ResultsProjection.BuildAsync` was read rather than trusted:

```csharp
var counted = await db.DayAnswers
    .Where(a => a.CandidateDay!.PollId == poll.Id)   // whole poll, no Skip/Take
    .GroupBy(a => new { a.CandidateDayId, a.Availability })

var raw = await db.Responses
    .Where(r => r.PollId == poll.Id)
    .Skip((current - 1) * PageSize).Take(PageSize)   // only the ROWS are paged
```

The counts are already whole-poll; only the response rows are paged. So a mark derived from `totals`
satisfies FR-003 **by construction**, and the requirement needs no mechanism of its own — only a
test that would notice if the projection ever changed.

**Alternatives considered**: *computing the best day on the server and sending it as a field.* It
would work, but it puts the system's opinion into the API contract, which then has to be versioned
and which the export would have to be kept out of (spec Assumptions). Deriving a display decision at
the point of display keeps it a display decision.

---

## R-2: The mark sits on the *yes* row's cell, not on all three

**Decision**: one mark per best day, in the cell of the **yes** row. The *maybe* and *no* cells of
that day are not marked.

**Rationale**: FR-001a makes the *yes* count the thing that decides, with *no* only breaking ties and
*maybe* not counting at all. Marking all three cells would suggest all three numbers are being
praised, which is the opposite of what the rule says — and it is exactly the misreading FR-007 exists
to prevent. One mark on the number that decided is the honest picture.

It also keeps the change inside three lines of an existing `v-for`, which is what the answer to
clarification Q2 asked for: nothing outside the summary rows changes.

**Alternatives considered**: *tinting the whole day column.* Reaches the response rows below, which
FR-008a forbids — the grid must look unchanged while folded, and a column tint would not fold away.

---

## R-3: Meeting "hover **and** keyboard focus" without littering the table with tab stops

**Decision**: the mark is a focusable element carrying the rule as its `title` and its accessible
name. It is rendered only on marked cells, so the number of new tab stops equals the number of best
days — normally one.

**This is the tension.** FR-007 asks for the rule on hover *and* on keyboard focus. Hover needs no
tab stop; focus does, and a table cell is not focusable on its own. The honest options were:

| Approach | Hover | Keyboard | Tab stops added |
|---|---|---|---|
| Visually hidden text only | no | no | 0 |
| `title` on the cell | yes | no | 0 |
| **Focusable mark inside the cell** | **yes** | **yes** | **one per best day** |

The third is the only one that satisfies FR-007 as written, and its cost is bounded by how many days
tie at the top. In the ordinary case that is one. The pathological case — fifty days tied, fifty tab
stops — is the case where the marks convey nothing anyway, and it is recorded here rather than
engineered around, because the engineering would be a lot of machinery for a poll nobody can use.

The project already has the pattern this leans on: `ShareLink.vue` carries a `d-sr-only` note and
points at it with `aria-describedby` rather than stuffing the text into the link's own name
(004 FR-016a). The same separation applies here — the mark is named "bester Tag", and the sentence
explaining *why* is a description, not part of the name.

**Alternatives considered**: *one shared explanation next to the fold control.* Rejected at
clarification: it would occupy space while folded, which FR-007 forbids and which clarification Q2
had already ruled out for the mark itself.

---

## R-4: "Best" is a pure function, so most of the feature is testable without a browser

**Decision**: the rule lives in one exported function taking the day totals and returning the set of
best day ids. The component renders what that function returns.

**Rationale**: FR-001a, FR-001b and FR-002 are arithmetic — most *yes*, then fewest *no*, at least
one *yes*, and every tied day. Arithmetic is where a subtle mistake hides best and where a rendered
test proves least: a component test that finds one marked cell says nothing about which of two tied
days was dropped. Every case in the specification's Edge Cases becomes one line of input and one
expected set.

Feature 002 already keeps its validation rules in a static `PollService.Validate` for the same
reason — "so the limits are testable without a database, and so there is exactly one place where
'enforced on the server' is true".

**Alternatives considered**: *computing it inline in the template.* Puts a rule that has three
clauses and two edge cases inside a `v-for` condition, where the only way to test it is to render it.
