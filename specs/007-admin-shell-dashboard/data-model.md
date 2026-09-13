# Data Model: Admin Shell, Dashboard and Settings

**Feature**: 007-admin-shell-dashboard
**Date**: 2026-09-12

## No schema change

This feature adds no table, no column, no index and no migration. Every figure is an aggregate over
rows that already exist, which is what FR-034 and Principle IV require. The existing entities are
unchanged:

| Entity | Role here |
|--------|-----------|
| `Poll` | `RetentionDeadline` drives figure 4 and the live-poll filter on all figures |
| `PollResponse` | counted for figure 2; its absence drives figure 3 |
| `DayAnswer` | grouped by `Availability` for figure 6 |
| `CandidateDay` | the join between `DayAnswer` and `Poll`; not counted itself |

`MaintenanceState` is read for figure 5 but not through this endpoint — see "Figure 5" below.

## The live-poll filter applies to every figure

Every figure is scoped by `RetentionService.LivePolls()` — `RetentionDeadline > now`. This is not
optional bookkeeping: 002 FR-039b makes the deadline the access filter, so a poll past its deadline
is already unreachable everywhere else in the system. A dashboard that counted it would contradict
the poll list on the same screen, which FR-029 forbids.

The consequence worth knowing: between a deadline passing and the hourly `RetentionSweep` erasing
the rows, the data is still on disk but must not be counted. The filter handles it; nothing else
needs to.

## The six figures

### Figure 1 — polls stored

```text
LivePolls().Count()
```

Zero is a legitimate reading and means "nothing stored". FR-031 requires the *view* to say that in
words rather than print `0`, so the endpoint reports the number and the view decides the wording.

### Figure 2 — answers across all polls

```text
Responses.Count(r => r.Poll.RetentionDeadline > now)
```

Counts responses, not day-answers: one person answering counts once regardless of how many days
they marked. This is the same number the poll list sums in its `responseCount` column, which is
what FR-029 requires them to agree on.

### Figure 3 — polls with no answer yet

```text
LivePolls().Count(p => !p.Responses.Any())
```

`!Any()` rather than `Count() == 0`, so SQLite can stop at the first row instead of counting all of
them per poll.

### Figure 4 — deletions due within seven days, and the earliest

```text
window   = now .. now + 7 days
dueCount = LivePolls().Count(p => p.RetentionDeadline <= windowEnd)
earliest = LivePolls().Min(p => (DateTimeOffset?)p.RetentionDeadline)
```

Two values from one concern, so they travel together. `earliest` is nullable and is `null` exactly
when no poll exists — distinct from "no poll is due soon", which is `dueCount == 0` with `earliest`
still set. The view must not collapse those two into one sentence.

`earliest` is the earliest deadline of *any* live poll, not only of those inside the window. That
way a dashboard with nothing due this week can still answer "when is the next one?" without a
second query, and the two readings never disagree about which poll is next.

### Figure 5 — maintenance mode (**not in this payload**)

Read from the existing `maintenance` store, which the shell already loads for the banner (FR-026).

Putting it in the dashboard payload as well would create two sources for one state, and FR-029
requires every figure to agree with what the corresponding area shows. A banner and a dashboard
tile contradicting each other on the same screen is the exact failure that would produce. One
source, read once by the shell, displayed in two places.

### Figure 6 — yes / maybe / no across every answer

```text
DayAnswers
  .Where(a => a.CandidateDay.Poll.RetentionDeadline > now)
  .GroupBy(a => a.Availability)
  .Select(g => new { g.Key, Count = g.Count() })
```

**Correct by construction, and this is the important property.** `DayAnswer` has three
`Availability` values and no fourth: an unanswered day stores no row at all (002 research R-8, and
the comment on `DayAnswer` says so). A grouped count therefore *cannot* count a non-answer, so:

- FR-028a's "must equal the sum of every poll's per-day summaries" holds because both are the same
  grouped count over the same rows — `ResultsProjection` filters to one poll, this does not filter
  at all beyond retention.
- "A poll with no answers contributes nothing rather than zeros" (FR-028a) needs no code. It has no
  rows to contribute.

There is no "exclude the unanswered" filter that could later be forgotten, which is the reason this
figure needs no defensive arithmetic.

**Absent groups are absent, not zero.** A system where nobody ever said *maybe* returns two groups,
not three. The projection maps missing groups to `0` for the wire, and the *view* then distinguishes
"three zeros because nobody has answered at all" (FR-028 scenario 8, say so in words) from "zero
*maybe* among 400 answers" (a real and interesting zero). The endpoint reports numbers; only the
view knows which story they tell.

**This figure carries the performance risk.** At FR-028c's documented worst case it touches 50
million rows. See `research.md` R-4 — measured before the endpoint is built, and SC-011 may be
amended as a result.

## Wire shape

```text
DashboardView {
  pollCount:          int        // figure 1
  responseCount:      int        // figure 2
  unansweredPolls:    int        // figure 3
  deletionsDueSoon:   int        // figure 4a — within 7 days
  nextDeletion:       string?    // figure 4b — ISO instant, null when no poll exists
  yes:                int        // figure 6
  maybe:              int        // figure 6
  no:                 int        // figure 6
}
```

Eight numbers, no nesting, no poll titles and no names — FR-034 forbids anything identifying on
this payload, and a flat record makes that visible at a glance rather than requiring a reader to
check what a nested object carries.

## Frontend state

### `stores/dashboard.ts` (new)

Mirrors the shape `stores/polls.ts` already uses, so the `unauthorized` redirect and the
`useProblemText` composable come along unchanged:

| Field | Meaning |
|-------|---------|
| `figures` | `DashboardView \| null` — `null` until a successful read |
| `loading` | a read is in flight |
| `loadProblem` | `ApiProblem \| null` — the last failure, if any |

The four states the view derives from those three fields, per R-7:

| State | Condition | Shown |
|-------|-----------|-------|
| loading | `loading && figures === null` | placeholders, no numbers |
| unreachable | `loadProblem && code !== 'unauthorized'` | storage wording, **no figures** (FR-032) |
| unauthorized | `loadProblem?.code === 'unauthorized'` | redirect to sign-in (FR-011) |
| empty | `figures.pollCount === 0` | words, not zeros (FR-031) |
| ready | otherwise | the six figures |

`unreachable` must render no figure at all. A partially-filled dashboard would be a set of claims
about data the system just said it could not read.

## Navigation model (frontend only, not persisted)

The navigation is a static list in `AdminNav.vue`. It is deliberately not data, not configuration
and not a registry — Principle III, and there are three entries.

| Order | Label (FR-002a) | Address | Catalogue key |
|-------|-----------------|---------|---------------|
| 1 | Dashboard | `/admin` | `nav.dashboard` |
| 2 | Terminfindungen | `/admin/terminfindungen` | reuses `poll.listTitle` |
| 3 | Einstellungen | `/admin/einstellungen` | `nav.settings` |

"Which entry is current" is not state we hold: Vue Router sets `aria-current="page"` on the active
link, and Vuetify's `v-list-item` renders that link (R-2). One source, and it is the one assistive
technology reads.

A poll's answers (`/admin/terminfindungen/:pollId`) has an address but no entry; entry 2 stays
current while it is shown (FR-014e).
