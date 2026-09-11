# Feature Specification: Highlighting the Best Days

**Feature Branch**: `006-highlight-best-days`
**Created**: 2026-09-06
**Status**: Draft
**Input**: User description: "Ich woll, das die besten Tage bei den Zusammenfassungen je Tag hervorgehoben werden sollen. Wenn es mehrere Gleiche Tage gibt, sollen allen hervorgehoben werden."

## Summary

The per-day summary already carries the numbers that answer *"which day works best?"* — the counts
of *yes*, *maybe* and *no* above each date. It does not answer the question; it supplies the raw
material and leaves the reader to compare columns. With a handful of days that is a moment's work.
With twenty it is a chore, and with a hundred — the documented maximum — it is the reason nobody
scrolls that far.

This feature makes the grid state the answer instead of implying it: the best day is marked. Where
several days are equally good, **all of them are marked** — the tie is the honest result, and
picking one arbitrarily would invent a winner the answers do not support.

### What "best" has to survive

Two problems make this less trivial than it looks, and both are in the clarifications below.

**A tally is not an order.** *Yes*, *maybe* and *no* are three separate counts, and turning them
into one ranking is a decision, not a calculation. Is a day with eight yes and six no better than
one with seven yes and none? Whatever the rule, it has to be stated, because the mark is the
system asserting something about other people's answers.

**The summary starts folded.** Feature 004 collapsed it deliberately (004 FR-003): the counts cost
no space until asked for. The mark lives inside those rows and appears with them, which keeps this
change entirely within what 004 built — no date, no header and no other part of the grid is
touched. The accepted consequence is that a reader who never unfolds the summary never sees a mark;
the feature helps whoever goes looking for the answer, and asks nothing of whoever does not.

## Clarifications

### Session 2026-09-06

- Q: What makes a day the best one? → A: **Most *yes*; where that ties, fewest *no*.** Two
  properties decided it: the rule states itself in one sentence, which FR-007 requires, and it
  invents no number. A weighting that counted *maybe* as half a *yes* would be the system asserting
  a constant nobody chose about other people's answers. Accepted consequence: *maybe* does not
  influence the ranking at all, so a day with 5 yes and 5 maybe loses to one with 6 yes and none,
  even though ten people could make the first. *Maybe* remains visible in the counts; it simply
  does not decide.
- Q: Is the mark visible while the summary is folded? → A: **No — the mark lives in the summary and
  appears when the summary is unfolded.** Nothing outside the summary rows changes: not the date,
  not the header, not the grid around them, so 004's layout stands untouched and this feature adds
  no permanent space. Accepted consequence: the answer is behind the same single action the counts
  are already behind, and a reader who never unfolds never sees it.
- Q: Must a day have at least one *yes* to be marked? → A: **Yes. Where no day has a *yes*, nothing
  is marked.** This resolves a contradiction that the rule from Q1 had created rather than managing
  it: with fewest-*no* as the tie-break, a day nobody answered (0 yes, 0 no) would have beaten a day
  everyone declined (0 yes, 5 no), which FR-009 forbids. Requiring a *yes* removes both from
  consideration instead. It also covers the newly created poll, which has no answers at all, and
  leaves the stated "mark every tied day" rule untouched for genuine ties.
- Q: Where is the rule explained? → A: **On the mark itself and nowhere else** — as its accessible
  name, and shown on hover and on keyboard focus. Nothing permanent is added to the interface, so a
  folded grid still looks exactly as it does today. A reader who wonders why one day is marked
  ahead of another asks the mark, and both a screen reader and a pointer get the same sentence.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read which day works best without comparing columns (Priority: P1)

Someone opens a poll with a dozen candidate days and several answers already in. The day that suits
most people is marked. They see it without reading a single number.

**Why this priority**: It is the whole feature. Everything else is what happens when the answer is
not a single day.

**Independent Test**: Create a poll where one day clearly leads, answer it, and confirm that day —
and only that day — is marked, in both the participant's view and the creator's.

**Acceptance Scenarios**:

1. **Given** a poll where one day has more support than every other, **When** the results are
   shown, **Then** that day is marked and no other day is.
2. **Given** a marked day, **When** a reader uses a screen reader, **Then** the mark is announced —
   it is not conveyed by colour alone.
3. **Given** a poll being answered, **When** a new response changes which day leads, **Then** the
   mark moves with it.
4. **Given** the creator's view and the participant's view of the same poll, **When** both are
   opened, **Then** the same day is marked in both.

---

### User Story 2 - See honestly when there is no single best day (Priority: P2)

Two days are tied at the top. Both are marked. Nobody is told a winner that the answers do not
support.

**Why this priority**: It is the case the request names explicitly, and the one where a naive
implementation quietly lies by picking the first of the tied days.

**Independent Test**: Answer a poll so that two days end up equal at the top, and confirm both are
marked and the remaining days are not.

**Acceptance Scenarios**:

1. **Given** two days tied at the top, **When** the results are shown, **Then** both are marked.
2. **Given** three days tied at the top of a poll with ten days, **When** the results are shown,
   **Then** exactly those three are marked.
3. **Given** a poll where a tie is broken by a later answer, **When** the results are shown again,
   **Then** only the day that now leads is marked.

---

### Edge Cases

- **No responses at all** — no day has a *yes*, so nothing is marked (FR-001b). This is what every
  poll looks like on the day it is created.
- **Nobody said yes to anything**, but there are answers: only *maybe* and *no* were given. Nothing
  is marked, whatever the *no* counts say.
- **Every day is tied at a non-zero count** — twenty days, everyone said yes to all of them: all
  twenty are marked, which is the stated rule applied honestly.
- **One candidate day**, which is therefore trivially the best.
- **A day nobody answered** while others were answered — absence is a state, not a zero someone
  chose (004 FR-006).
- **A poll at the documented limits**: 100 days and 1000 responses, where the marks must survive
  sideways scrolling with their own column — they sit in the summary rows, which already scroll
  with their day (004 FR-001a).
- **The summary is folded and then unfolded again**: the mark must be there both times, computed
  from the same answers, rather than only on the first unfolding.
- **The results are paged**: the mark must reflect the whole poll, not the page on screen. A "best
  day" computed from one page of responses would change as the reader pages through.
- **A response is deleted by the creator**, changing which day leads.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST mark the day or days that are best, in the per-day summary of the
  results.
- **FR-001a**: A day is best when it has at least one *yes* and no other day has more *yes*. Where
  several days share the highest *yes* count, the ones among them with the fewest *no* are best.
  *Maybe* does not enter the comparison — it is shown, not counted.
- **FR-001b**: Where no day has a single *yes*, no day is marked. This covers a poll nobody has
  answered yet and one where only *maybe* and *no* were given.
- **FR-002**: Where several days are equally best, **all** of them MUST be marked. No tie may be
  broken by position, by date, or by any rule the reader cannot see.
- **FR-003**: The mark MUST be derived from every response to the poll, not from the responses
  currently on screen. Paging through responses MUST NOT change which day is marked.
- **FR-004**: The mark MUST appear identically in the participant's view and the creator's view.
- **FR-005**: The mark MUST NOT be conveyed by colour alone. It MUST be perceivable without colour
  and MUST be announced to assistive technology.
- **FR-006**: The mark MUST update when the answers change — a new response, a revised one, or one
  the creator deleted.
- **FR-007**: The rule from FR-001a MUST be carried by the mark itself: as the mark's accessible
  name, and revealed on hover and on keyboard focus. It MUST NOT occupy permanent space anywhere in
  the interface. A reader who sees a day with fewer *maybe* answers marked ahead of one with more
  must be able to find out why without guessing — and without a mouse, which is why focus counts as
  well as hover.
- **FR-008**: Marking MUST NOT change, reorder or hide any count, any day, or any response. It adds
  emphasis and nothing else.
- **FR-008a**: The mark MUST live within the per-day summary and MUST appear and disappear with it.
  Nothing outside the summary — the date, the grid header, the response rows — may change
  appearance, so a folded grid looks exactly as it does today (004 FR-003).
- **FR-009**: A day that nobody answered MUST NOT be marked ahead of a day that people declined —
  absence is not agreement. FR-001b makes this true by construction rather than by a second rule:
  neither day has a *yes*, so neither is a candidate. Stated here because the constraint is what
  FR-001b exists to satisfy, and a later change to either would otherwise silently drop it.

### Key Entities

- **Best day**: A candidate day that no other day beats under the stated rule. There may be
  several; there may, in the cases under Edge Cases, be none.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With the summary unfolded, a reader can name the best day of a poll with 20 candidate
  days in under 5 seconds, without reading any count. The unfolding itself is one click and is not
  counted against the five seconds — it is the action 004 already requires to see any count at all.
- **SC-002**: Where days are tied at the top, 100% of the tied days are marked — verified across
  ties of two, three and all days.
- **SC-002a**: A poll in which no day has a *yes* shows no mark at all — verified for a poll with no
  responses and for one answered only with *maybe* and *no*.
- **SC-003**: The marked day is the same in the participant's view and the creator's view in 100%
  of cases.
- **SC-004**: The mark is perceivable with colour removed, and is announced by a screen reader in
  100% of marked days.
- **SC-004a**: The rule behind the mark is reachable from the mark alone — by screen reader, by
  hover and by keyboard focus — without any explanatory text occupying space in the folded or
  unfolded grid.
- **SC-005**: Paging through the responses of a poll at the documented maximum never changes which
  day is marked.
- **SC-006**: No count, day or response shown before this feature is changed by it — verified field
  by field against the current results view.

## Assumptions

- **The rule applies to whole polls, not to expired ones.** Polls past their retention deadline are
  already unreachable (002 FR-039b); nothing here changes that.
- **The mark is shown wherever the per-day summary is shown**, which today is the results grid on
  both the participant's and the creator's view.
- **No new data is stored.** The best day is derived from the counts that already exist; there is
  nothing to persist and nothing to migrate.
- **The export is unchanged.** It carries the answers, not the system's opinion about them; a
  consumer computing its own ranking must not find one baked in.
- **The rule is fixed, not configurable.** A per-poll choice of ranking would be a different and
  much larger feature, and no request has been made for one.
- **German wording follows the existing interface**, with the text living in the same message
  catalogue as everything else the reader sees — including the sentence that states the rule, which
  is user-facing text and not a code comment.
- **A touch-only reader reaches the rule less easily** than one with a pointer or a keyboard, since
  there is no hover to perform. This is the accepted cost of adding no permanent explanatory text;
  the mark still announces itself, and the counts it is derived from are on screen beside it.
