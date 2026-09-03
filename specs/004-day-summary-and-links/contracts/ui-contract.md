# UI Contract: Day Summary and Followable Addresses

**Feature**: 004-day-summary-and-links | **Date**: 2026-09-03

There is no API change in this feature — no route, no payload, no field (FR-020). What it does
expose is an interface a person and a test both read: which elements exist, what identifies them,
what state they carry, and what assistive technology is told. That is the contract here.

Identifiers are `data-testid` values, as everywhere else in this project: tests address behaviour
by identifier and never by German text, so a changed translation cannot break a test.

---

## The results grid

### Document order

Top to bottom, inside the results card:

```text
  heading + response count            (unchanged)
  summary-toggle                      NEW  - the fold-out control
  table
    thead
      summary-row[state=yes]          MOVED from tfoot, only while unfolded
      summary-row[state=maybe]        MOVED from tfoot, only while unfolded
      summary-row[state=no]           MOVED from tfoot, only while unfolded
      the row carrying the dates      (unchanged)
    tbody
      result-row *                    (unchanged)
    tfoot                             REMOVED
```

The three summary rows precede the date row and are inside the same table (FR-001). That is what
keeps each summary over its own column when the grid scrolls sideways (FR-013) — the table does
it, nothing implements it.

### `summary-toggle`

| | |
|---|---|
| Element | A button, reachable by keyboard |
| Position | Directly above the table, left aligned |
| State | `aria-expanded` — `"false"` on arrival, `"true"` while unfolded |
| Name | From the translations, never a literal (FR-021 of 002) |
| Present when | The poll has at least one response |
| Absent when | The poll has no responses (FR-012) |

`aria-controls` is deliberately absent. The rows it would name do not exist while folded, and a
reference to nothing is worse than an omitted optional attribute — see research R-2, where the
attribute was measured to accept a list of ids before being rejected.

### `summary-row`

One per state, in the order *yes*, *maybe*, *no* (FR-001).

| | |
|---|---|
| Identifier | `summary-row` |
| State | `data-state` — `yes` \| `maybe` \| `no` |
| First cell | The state's name and its mark, in the column that names participants (FR-001a) |
| Remaining cells | One per candidate day, in the same order as the date row, carrying that day's count |
| Exists when | Unfolded |
| Does not exist when | Folded — removed from the document, not merely hidden (FR-006, research R-3) |

The count is a whole number, zero included (FR-011). It counts every response to the poll, not
only the page of fifty currently shown (FR-008a). The three numbers therefore need not add up to
the number of responses: a day nobody answered is counted in none of them (FR-009).

### What is removed

`totals-row` in the table footer, and the footer with it (FR-002). Its identifier does not move to
the new rows: `summary-row` is a different thing in a different place, and reusing the name would
let a test that was never updated pass against something it was not written for.

---

## Addresses

Three places show one (FR-015). All three behave identically.

| Identifier | Where | Points at |
|---|---|---|
| `poll-list-link` | Each card in the creator's list | That poll's participant view |
| `poll-share-link` | The creator's confirmation after creating a poll | That poll's participant view |
| `share-url` | The participant's confirmation after answering | That participant's own answer |

Each is now an anchor with:

| | |
|---|---|
| Destination | The full address, same origin |
| Target | A new tab |
| Relationship | Marked so the opened page gets no handle on the tab that opened it (FR-016b) |
| Accessible name | The address, followed by a visually hidden note that a new tab will open (FR-016a) |
| Text content | Unchanged — the bare address, selectable and copyable (FR-017) |

**The text content is part of the contract.** Six existing tests read these elements with
`textContent()`. An anchor carries the same text as the element it replaces, so all six keep
passing untouched — the change is additive to what is already asserted, and any drift here would
be a regression, not a detail.

The copy control beside each address (`share-copy`) is unchanged and still yields the same
address.

---

## What a reader is told

| Situation | Expected |
|---|---|
| Arriving at a poll with answers | No count is announced; the control announces itself as collapsed |
| Unfolding | The counts enter the reading order; the control announces itself as expanded |
| Folding again | The counts leave the reading order entirely |
| Following an address | The link's name says a new tab will open, before it opens |
| A poll with no answers | Neither the control nor the counts exist; the existing empty-state message stands alone |

---

## Unchanged by this feature

Every cell mark and its screen-reader label; the paging control and its page size; the empty
state; the delete control in the creator's view; every colour; every route; every request; every
stored value.
