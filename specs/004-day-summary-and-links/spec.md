# Feature Specification: Day Summary Above the Date, and Links That Are Links

**Feature Branch**: `dev` (feature directory `004-day-summary-and-links`; no feature branch per constitution v2.0.1)
**Created**: 2026-09-03
**Status**: Draft
**Input**: User description: "Die Zusammenfassung der ausgewählten Antworten sollten pro Tag oberhalb des Datums und nicht unterhalb aller Antworten stehen. Es sollte aber zunächst in einem Akkordeon versteckt sein. Weiterhin sollte in der Adminseite der Link zu den Umfragen auch als klickbarer Link umgesetzt sein."

## Summary

Two changes to what is already there, neither of which adds data.

The per-day tally of *yes / maybe / no* currently sits **beneath** every response, at the foot of
the grid. With many participants it ends up below the fold, so the one question the grid exists to
answer — *which day works best?* — is the last thing a reader reaches. It moves to sit **above**
each day's date, and starts **collapsed**: the answer is where you look for it, and it costs no
space until you ask for it.

Second: the participant link shown in the admin area is text that looks like a link and behaves
like a paragraph. It becomes a link that can be followed.

## Clarifications

### Session 2026-09-03

- Q: Does "collapsed at first" apply to the participant view as well, or only to the creator's? → A: Both start collapsed — one behaviour, one rule.
- Q: What shape does a day's summary take above its date — the three labelled rows as today, one compact line, or the yes-count alone? → A: The three rows as they are today, moved above the date.
- Q: Does a poll link open in the same tab or a new one? → A: A new one, so the admin area is left standing.
- Q: Which displayed addresses become followable — all three, the admin area only, or the poll list only? → A: All three, including the participant's personal link.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The tally is at the top, and out of the way until wanted (Priority: P1)

Someone opens a poll with a couple of dozen answers. They want to know which day is winning. Today
they scroll past every response to reach the tally at the bottom. After this change the tally
belongs to the day it describes and sits directly above that day's date — folded away on arrival,
one action to unfold.

**Why this priority**: it is the reason the grid exists. Everything else on the page supports the
question the tally answers.

**Independent Test**: create a poll, record several answers, open the results — no counts are
visible, and the date row is the first thing under the heading. Unfold, and each day's counts
appear directly above that day's date. The counts below the responses are gone.

**Acceptance Scenarios**:

1. **Given** a poll with answers, **When** the results are opened, **Then** no per-day counts are
   visible and nothing has to be scrolled past to reach the dates.
2. **Given** the collapsed summary, **When** the reader unfolds it, **Then** every day shows its
   count of *yes*, *maybe* and *no* directly above that day's date.
3. **Given** the unfolded summary, **When** the reader folds it again, **Then** the counts
   disappear and the grid is exactly as it was on arrival.
4. **Given** a poll with answers, **When** the results are read from top to bottom, **Then** no
   tally appears below the responses.
5. **Given** a day nobody answered while others did, **When** the summary is unfolded, **Then**
   that day shows three zeros rather than nothing.
6. **Given** a reader using only a keyboard, **When** they reach the control, **Then** they can
   unfold and fold it, and the control says which of the two states it is in.
7. **Given** a participant who opens a poll link with no account, **When** the page loads,
   **Then** the summary is folded there too, and the same single action unfolds it.

---

### User Story 2 - An address that looks like a link is one (Priority: P2)

The creator wants to see what participants see — or simply to open the poll they just made. The
address is right there on the card. Clicking it does nothing, because it is text.

**Why this priority**: small, self-contained, and it removes a daily irritation. It does not
depend on US1 and could ship on its own.

**Independent Test**: create a poll, click the address shown on its card in the list, and land on
the participant view of that poll.

**Acceptance Scenarios**:

1. **Given** a poll in the admin list, **When** the creator clicks its address, **Then** the
   participant view of that poll opens.
2. **Given** the creator has followed such a link, **When** they switch back to the admin tab,
   **Then** it is untouched: still signed in, same position, same open results.
3. **Given** an address shown anywhere in the system, **When** the reader selects the text instead
   of clicking, **Then** the full address is still selectable and copyable, and the copy control
   still works.
4. **Given** a participant who has just submitted an answer, **When** they click their personal
   address, **Then** their own answer opens, in a new tab, ready to be revised.

---

### Edge Cases

- **A poll nobody has answered yet**: there is nothing to summarise. The existing empty-state
  message stays, and no fold-out control appears — a control that unfolds to three zeros in every
  column is furniture, not information.
- **A day nobody chose while other days were answered**: shown as zeros. Absent counts and zero
  counts must not look the same, because one of them means "nobody could" and the other means
  "nobody was asked".
- **A poll at the declared limits** (100 days, 1000 responses): the summary belongs to its day
  column and must move with it when the grid is scrolled sideways. A summary that stayed put while
  the dates scrolled would attribute counts to the wrong day.
- **The unfolded summary is three rows tall**: unfolding pushes the responses down by three rows,
  not by one. That is the accepted cost of keeping the familiar shape, and it is the reason the
  summary starts folded rather than merely being moved.
- **Keyboard only**: the fold-out is reachable and operable without a pointer.
- **Screen reader**: while folded, the counts are not announced — a control that hides content
  visually but leaves it in the reading order has hidden nothing.
- **Without colour**: the three counts stay distinguishable, as the cells already are.
- **The creator follows a poll link and comes back**: the session survives; nothing has to be
  entered again.
- **A poll link for a poll that expires while the list is open**: following it produces the same
  neutral not-found any other unusable link produces. Nothing new is disclosed by the link being
  clickable.

## Requirements *(mandatory)*

### Functional Requirements

**The per-day summary**

- **FR-001**: The per-day counts of *yes*, *maybe* and *no* MUST be shown above the date of the
  day they describe, as **three rows** — one per state, in that order — each carrying the state's
  own name and mark on the left, exactly as they read today at the foot of the grid.
- **FR-001a**: The left-hand labels of those three rows MUST line up with the column that names
  the participants, so a number can be traced to its state by looking left and to its day by
  looking down. This is the only thing that makes three stacked numbers readable.
- **FR-002**: The per-day counts MUST NOT be shown below the responses. The existing tally at the
  foot of the grid is removed, not duplicated.
- **FR-003**: The summary MUST be collapsed when the results are first shown, on **both** the
  creator's view and the participant view. There is one behaviour, not one per audience: a rule
  that differed by who was looking would have to be explained every time someone asked why.
- **FR-004**: A single control MUST fold and unfold the summary for all days together. Days are
  not folded individually: comparing one day against another is the whole purpose, and a reader
  who has to unfold each column separately cannot compare anything.
- **FR-005**: The control MUST state whether the summary is currently folded or unfolded, and MUST
  be operable by keyboard alone.
- **FR-006**: While folded, the counts MUST NOT be reachable by keyboard or announced by a screen
  reader.
- **FR-007**: Folding and unfolding MUST NOT re-request anything and MUST NOT change any stored
  data.
- **FR-008**: The fold state MUST NOT be remembered between visits. Every arrival starts folded —
  remembering it would mean storing something about the reader, and the reader is deliberately
  anonymous (Principle I).
- **FR-008a**: The counts MUST cover every response to the poll, not only the responses on the
  page currently shown. The grid pages at fifty rows; the summary does not, and never has.
- **FR-009**: The counts MUST keep their present meaning: three counted states, with an
  unanswered day counted in none of them (002 FR-033). The three numbers therefore still need not
  add up to the number of responses.
- **FR-010**: Each count MUST remain identifiable without colour (002 FR-053).
- **FR-011**: A day with no answers MUST show zeros rather than an empty space.
- **FR-012**: When a poll has no responses at all, neither the summary nor its control MUST be
  shown.
- **FR-013**: Each day's summary MUST stay aligned with that day's column when the grid is
  scrolled sideways (002 FR-036c).
- **FR-014**: The change MUST apply wherever the results grid is shown — the participant view and
  the creator's view are the same grid and MUST NOT diverge.

**Links that can be followed**

- **FR-015**: **Every** address the system displays MUST be a link that can be followed. There are
  three: the poll address on each card in the creator's list, the poll address shown right after a
  poll is created, and the participant's personal address shown right after an answer is submitted.
  One rule — what looks like a link is one.
- **FR-015a**: The participant's personal address MUST be included even though the request named
  the admin area, because it is the address that matters most: with no account, it is the only way
  back to one's own answer (002 FR-026).
- **FR-016**: Such a link MUST open in a new browser tab, leaving the admin area exactly as it
  was — its scroll position, any open results and any unfolded summary intact. Returning is a tab
  switch, not a reload.
- **FR-016a**: That a link opens in a new tab MUST be announced to assistive technology, not only
  implied by what happens. An unannounced context switch leaves a screen-reader user reading a
  page they did not ask for and unable to find the way back.
- **FR-016b**: The opened page MUST NOT be given any handle on the tab that opened it.
- **FR-017**: The full address MUST remain visible and selectable as text, and the existing copy
  control MUST keep working. People paste these into a chat window far more often than they click
  them.
- **FR-018**: Following a link MUST NOT disclose anything a person with the address could not
  already see. A clickable link is a convenience, not a new capability.

**Boundaries**

- **FR-019**: No new data MUST be stored, and no stored data MUST change shape. Both changes are
  presentational.
- **FR-020**: No request MUST be added or removed. The numbers shown are the ones already
  delivered with the results.
- **FR-021**: Every existing test MUST still pass, with no assertion weakened. Where a test
  asserted the tally's old position, it MUST be re-pointed at the new one rather than deleted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On arrival at a poll with answers, zero per-day counts are visible and the dates are
  visible without scrolling — measured on the creator's view and on the participant view.
- **SC-002**: After one action, every day's counts are visible, each vertically above its own
  date.
- **SC-003**: Zero counts appear below the responses.
- **SC-004**: The fold-out can be operated, and both states confirmed, using the keyboard alone.
- **SC-005**: With 100 days and 1000 responses, unfolding is complete within 1 second and the page
  body still does not scroll sideways.
- **SC-006**: From the admin list, a poll's participant view is reachable in one click; it opens
  in a new tab, and the admin tab is unchanged when returned to — same scroll position, same open
  results, same fold state — in 100% of attempts.
- **SC-007**: The full address remains selectable and the copy control still yields the same
  address it does today.
- **SC-008**: Every test that passed before this feature passes after it, with zero assertions
  weakened.

## Assumptions

- **"Zusammenfassung" means the counts that exist today** — *yes*, *maybe* and *no* per day. No
  new figure (a "best day", a percentage, a ranking) is introduced; the request was about where
  the existing numbers sit, not what they are.
- **One control for all days.** The description says "in einem Akkordeon" — one, singular. Per-day
  fold-outs are read as not intended, and FR-004 records why they would also be worse.
- **No preference is stored**, so no storage, no cookie and no account is involved (FR-008).
- The results grid and the participant view are unchanged in every other respect; this feature
  moves and hides existing figures and turns existing text into links.

## Out of Scope

- Sorting or highlighting days by their result (for example marking a winning day). That is a
  different feature and a different decision about what "winning" means.
- Remembering the fold state per reader or per poll.
- Any change to what is counted, how it is counted, or what is stored.
- Exporting the summary separately — the export already carries every answer (003 FR-014), from
  which any tally can be recomputed.
