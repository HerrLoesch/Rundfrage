# Phase 0 Research: Admin Shell, Dashboard and Settings

**Feature**: 007-admin-shell-dashboard
**Date**: 2026-09-12
**Spec**: [spec.md](./spec.md)

This feature is mostly a rearrangement of code that already works, and most of its decisions are
therefore about *where* things live rather than what they do. Two questions are genuinely open and
carry risk: how the dashboard's answer distribution is obtained (R-3), and whether the criterion we
wrote for it is achievable at the scale we wrote (R-4). R-4 is the finding that matters most in this
document and the only one that may send us back to the spec.

---

## R-1: The shell is a parent route, not a component every area imports

**Decision**: Introduce one `AdminShell.vue` as a parent route at `/admin` whose `children` are the
areas. The shell owns the navigation drawer, the app bar, the sign-out control and the maintenance
banner; each child renders into a nested `<RouterView>`.

**Rationale**: FR-001 requires the navigation bar to persist *unchanged* while the content changes.
A nested route gives that for free — Vue Router keeps the parent mounted across child navigations,
so the drawer keeps its scroll position and its open/closed state without anyone managing it.
It also makes FR-008 structural rather than remembered: the shell wraps exactly the routes that
need a session, and the sign-in form sits outside it, so there is no per-area decision to forget.

**Alternatives considered**:

- *Each area imports a `<AdminNav>` of its own.* Rejected: the navigation would remount on every
  navigation, and FR-001's "persisting unchanged" would be a coincidence of re-rendering rather
  than a property. Four copies of the same import is also exactly the duplication Principle III
  warns about.
- *Keep today's approach — `App.vue` switches chrome on `route.path.startsWith('/admin')`.*
  Rejected: it already reads as a special case at one level, and it does not scale to a drawer,
  a banner and a sign-out. The conditional would grow a second and third branch for no gain.

---

## R-2: `v-navigation-drawer` + `v-list nav`, permanent above the `md` breakpoint

**Decision**: `v-navigation-drawer` with `v-list nav` inside; entries are `v-list-item` with `:to`.
The drawer is `permanent` from Vuetify's `md` breakpoint up, and `temporary` below it, toggled by a
`v-app-bar-nav-icon` in the app bar.

**Rationale**: FR-012 needs the navigation reachable on a narrow screen without covering the
content once a destination is chosen; `temporary` closes on selection, which is that behaviour
exactly. For FR-013 and SC-008 the important detail is that `v-list-item` with `:to` renders a
`RouterLink`, and Vue Router sets `aria-current="page"` on the active link itself — so "which entry
is current" is announced without an `aria-current` we write and could get wrong. `v-list nav`
already wraps the items in a `<nav>` landmark, so the region is reachable by landmark navigation.

**Alternatives considered**:

- *`v-tabs` along the top.* Rejected: the request asks for a bar on the left, and tabs do not hold
  a growing list of areas — the middle section is explicitly meant to grow (FR-004).
- *A rail of icons only.* Rejected: FR-002a fixes text labels, and an icon-only rail would make the
  accessible name the only name, which is worse for the German labels we chose.
- *Writing `aria-current` by hand.* Rejected on the reasoning that Vue Router already emits it on
  the active link, and two sources risk disagreeing.

  **Corrected during implementation (T011/T022).** The reasoning was right and the conclusion was
  wrong, because the router alone cannot express what FR-002 and FR-014e need together:

  - `v-list-item` does **not** derive `aria-current` from Vuetify's `active` prop. The attribute
    comes from the underlying link's own route matching, which is *inclusive*.
  - Inclusive matching marked `/admin` as current on every address beneath it, so two entries
    carried `aria-current` at once — precisely what FR-002 forbids.
  - Making it exact fixed that and broke FR-014e: with one poll's answers shown, no entry was
    marked, because `poll-answers` is a **sibling** route record of `polls`, not a descendant, and
    Vue Router derives "active" from record ancestry rather than from path prefixes.

  So each entry now names the route names it owns, and that single list drives both `active` and
  an explicit `aria-current` (which overrides Vuetify's, verified). There is still exactly one
  source for what a screen reader reads — it is the route name rather than the router's own
  matching. The original concern stands; it just pointed at the wrong implementation.

---

## R-3: The six figures come from one endpoint and five fixed aggregate queries

**Decision**: One new route, `GET /api/v1/admin/dashboard`, returning figures 1–4 and 6 of FR-028.
Figure 5 (maintenance) is **not** in the payload: the frontend reads it from the existing
`maintenance` store, which the shell already needs for the banner (FR-026).

The five server-side queries are all fixed in number — they do not grow with the number of polls:

| Figure | Query |
|--------|-------|
| 1. polls stored | `LivePolls().CountAsync()` |
| 2. answers total | `COUNT` over `Responses` joined to live polls |
| 3. polls unanswered | `LivePolls().CountAsync(p => !p.Responses.Any())` |
| 4. deletions within 7 days + earliest | `COUNT` + `MIN(RetentionDeadline)` over live polls in window |
| 6. yes / maybe / no | `GROUP BY Availability` over `DayAnswers` joined to live polls |

**Rationale**: FR-028b forbids reading each poll's answers in turn, and five fixed queries satisfy
that by construction — the count of round trips is the same for 3 polls and 500.

Figure 6 is correct *by construction* rather than by careful filtering, and this is worth stating
because it is the reason the figure is cheap to get right: `DayAnswer` stores no row for an
unanswered day (there is no fourth `Availability`, per 002 research R-8). So a `GROUP BY
Availability` cannot count a non-answer, which is exactly what FR-028a demands — the aggregate
equals the sum of every poll's per-day summary because both are the same grouped count over the
same rows, one filtered per poll and one not. No "exclude the unanswered" filter exists to be
forgotten, and FR-028a needs no defensive code.

Keeping maintenance out of the payload is the other half of the decision. The shell must read the
store anyway for FR-026, and putting the same state in the dashboard payload would create two
sources that FR-029 requires to agree — a disagreement that would show as a banner and a dashboard
tile contradicting each other on the same screen.

**Alternatives considered**:

- *Six separate endpoints, one per figure.* Rejected: six round trips against SC-011's two seconds,
  and six chances for a partial failure to produce a dashboard that is half figures and half error.
- *Derive figures 1–4 on the client from the existing `GET /admin/polls` list.* Tempting, because
  the list already carries `responseCount` and `retentionDeadline` per poll, and it would need no
  new endpoint at all. Rejected because figure 6 cannot come from it, so the dashboard would need
  a request either way — and then splitting four figures onto the client and two onto the server
  puts FR-029's "must agree" burden on two different pieces of arithmetic.
- *A stored running total per poll, updated on submit and delete.* Rejected by the spec itself
  (Assumptions, and Principle IV's ban on recording more than the answers). It also has a failure
  mode the current design cannot have: every deletion path — response delete, poll delete, restore,
  import, and the hourly retention sweep — would have to decrement it correctly, and the one that
  forgot would leave a figure that is wrong forever with nothing to detect it.

---

## R-4: SC-011 is very likely unachievable at the scale FR-028c states — measure before building

**This is the finding that may require a spec change, and it is the author's own fault: FR-028c and
SC-011 were both added during clarification on 2026-09-12, and their combination was not costed.**

**The problem**: FR-028c documents the supported scale as 500 polls, each at 002 FR-015's limits of
1000 answers across 100 candidate days. Multiplied out, the worst case is:

| Table | Worst-case rows |
|-------|-----------------|
| `Polls` | 500 |
| `PollResponse` | 500,000 |
| `DayAnswer` | **50,000,000** |

Roughly 2 GB of SQLite. SC-011 asks for all six figures on screen within two seconds. Figure 6's
`GROUP BY Availability` has to touch every one of those 50 million rows — SQLite has no way to
count a group without visiting its members, and the join to `Polls` for the retention filter
prevents an index-only scan over `Availability` alone. Two seconds is not a realistic budget for
that, and no index fixes it, because the cost is the row count and not the lookup.

Figures 1–4 are unaffected: they are counts over 500 polls and 500,000 responses, both indexed.
Figure 6 is the whole problem.

**MEASURED 2026-09-12** (T001–T003, `backend/tests/Rundfrage.Api.IntegrationTests/DashboardScaleTests.cs`):

| DayAnswer rows | Seed | Cold read | Warm read | Storage on disk |
|---------------:|-----:|----------:|----------:|----------------:|
| 1,000,000 | 3.3 s | 0.27 s | 0.24 s | 218 MiB |
| 2,500,000 | 8.8 s | 0.66 s | 0.67 s | 547 MiB |
| 5,000,000 | 22.5 s | 1.71 s | 1.37 s | 1,097 MiB |

The cost is **linear** in the row count — about **0.27 s and 219 MiB per million rows** — which is
what a full scan should look like and means the criterion can be stated honestly for whatever scale
the product actually reaches. Two consequences follow arithmetically:

- SC-011's two-second budget is exhausted at roughly **7.3 million** day-answers.
- FR-028c's worst case of 50,000,000 extrapolates to about **14 seconds** and about **10.7 GB** on
  disk.

The disk figure turned out to matter more than the timing. An 11 GB SQLite file contradicts the
constitution's own reason for choosing SQLite: PostgreSQL was rejected because a 415 MB image and a
23 MB resident server "outweighed everything it stores — of which the actual polls are a few
kilobytes". A scale whose database is 11 GB is not a scale this product has. FR-028c described a
number that is arithmetically possible and practically meaningless.

**Outcome: R-4 resolves to branch T004b.** The design in R-3 is correct and stays; **SC-011 and
FR-028c were wrong and are amended** (see spec.md Clarifications, session 2026-09-12, second
entry). The amended scale is 500 polls and up to 2,500,000 day-answers in total — for example 500
polls averaging 100 responses across 50 candidate days, which is already generous for a group
date-finding tool. That measures 0.66 s cold, leaving roughly a threefold margin under two seconds
for hardware slower than the development machine, which matters because this is self-hosted
software and the measuring machine is not the deploying one.

**What was rejected, again and on measurement rather than principle**: making figure 6 fast. At
0.27 s per million the only way to beat a full scan is to avoid it, which means a maintained total
— and that is what Principle IV and this document already refused. The measurement did not change
that argument; it changed the criterion.

**What we will not do**: leave SC-011 in the spec unmeasured and discover it in review. The
constitution's gate 2 requires the suite green, and a performance criterion nobody measured is a
criterion that is not green, merely unasserted.

---

## R-5: Addresses stay German, and `/admin` *is* the dashboard

**Decision**:

| Address | Area |
|---------|------|
| `/admin` | Dashboard (the shell's index child) |
| `/admin/terminfindungen` | Date-poll area — the poll list |
| `/admin/terminfindungen/:pollId` | One poll's answers |
| `/admin/einstellungen` | Settings |
| `/admin/anmelden` | Sign-in — outside the shell |
| anything else under `/admin` | redirect to `/admin` |

**Rationale**: the existing admin address is already German (`/admin/anmelden`), so German paths
continue a decision rather than making a new one. Making `/admin` itself the dashboard satisfies
FR-006 structurally — "entering the admin area without naming an area" is just the index route, so
there is no redirect to write — and it means the operator's existing `/admin` bookmark still works
and lands on the dashboard, which is precisely what FR-006 and FR-011a already specify.

`MapFallbackToFile("index.html")` is already in `Program.cs`, so every new address survives a
reload with no backend change. SC-006 costs nothing.

**Alternatives considered**:

- *`/admin/uebersicht` for the dashboard, with `/admin` redirecting to it.* Rejected: a redirect to
  write and test, in exchange for an address nobody types. It would also break the `/admin`
  bookmark into a redirect hop for no benefit.
- *English paths (`/admin/polls`, `/admin/settings`).* Rejected: `/admin/anmelden` is already
  German, and a mixed scheme is the kind of inconsistency that gets "tidied" later by someone
  guessing which half was intended.

---

## R-6: The maintenance banner moves to the shell and the switch loses it

**Decision**: `AdminShell.vue` renders the banner from the `maintenance` store.
`MaintenanceSwitch.vue` keeps the toggle and its asymmetric confirmation (FR-023) and **no longer
renders the banner at all**.

**Rationale**: FR-026a requires the banner's presence to depend only on maintenance being on — not
on the switch being rendered. Today the banner is a child of the switch, so moving the switch to
settings would carry the warning out of every area the operator actually works in, which is the
opposite of what FR-026 is for. Both stores already exist and the banner's wording is already in
the catalogue (`maintenance.bannerTitle`, `bannerBody`, `maintenance.since`), so this is a move,
not a rewrite.

**The coupling worth naming**: lifting the banner and relocating the switch are one change, not
two. Do them in separate tasks and there is an intermediate state with the switch in settings and
the banner nowhere — FR-026 violated by a commit that passes its own tests.

**Alternatives considered**:

- *Leave the banner in the switch and additionally render a second banner in the shell.* Rejected:
  two components claiming the same state, and the settings page showing the warning twice.
- *A badge on the settings nav entry instead.* Rejected during clarification (Q2, session 2) as too
  easy to overlook for the failure it guards.

---

## R-7: Loading, empty and unreachable are three states, and the dashboard needs all three

**Decision**: A new `dashboard` Pinia store mirroring the `polls` store's shape: `figures`,
`loading`, and `loadProblem` (an `ApiProblem | null`). The view distinguishes:

- **loading** — skeleton placeholders, no numbers;
- **unreachable** (`loadProblem` set and not `unauthorized`) — the storage-unavailable wording,
  **no figures at all** (FR-032);
- **nothing stored** (figures present, zero polls) — words, not zeros (FR-031);
- **nothing answered** (polls exist, no answers) — the distribution says so rather than showing
  three zeros (FR-028 scenario 8);
- **unauthorized** — push to sign-in, as the poll list does today (FR-011).

**Rationale**: FR-031 and FR-032 are the two states this project has been careful about since
003, and the dashboard makes the distinction sharper: a *list* showing nothing is ambiguous, but a
*figure* showing `0` is a positive claim about the data. Reusing the existing store shape means the
existing `useProblemText` composable and the `unauthorized` redirect come along unchanged.

The loading state is new to this project only in that no previous view had numbers worth a
placeholder. It matters here because SC-011 permits up to two seconds, and two seconds of blank
tiles reads as zeros.

**Alternatives considered**:

- *One `ref` per figure in the component.* Rejected: six refs, six loading flags, and no single
  place where "the read failed" is true. The store shape already exists and already has the
  `unauthorized` handling this view needs.

---

## R-8: The answers page is a route, and a missing poll lands on the list

**Decision**: `/admin/terminfindungen/:pollId` loads via the existing
`GET /api/v1/admin/polls/{pollId}` and renders `ResultGrid` with `deletable`. A `404` — which that
endpoint already returns as a neutral not-found — redirects to `/admin/terminfindungen` with a
message saying the poll is not there (FR-014g).

**Rationale**: no backend change is needed; the endpoint and its neutral 404 already exist for
today's inline expansion. The redirect target is the list rather than the dashboard because the
list is where the operator can see what *does* exist, and FR-014g says so explicitly.

Per-response deletion re-reads the poll afterwards, exactly as `PollList` does today, because the
per-day totals move with it and the server is the only thing that knows the new numbers. Deleting
the last answer leaves the operator on the page showing the grid's empty state (FR-014f) — which
falls out of re-reading rather than needing a special case.

**Alternatives considered**:

- *Pass the already-loaded poll summary through router state to save a request.* Rejected: it would
  make a reloaded address behave differently from a clicked one, and FR-014a requires the address
  to work on its own.

---

## R-9: Nine of ten end-to-end specs move, and that is the bulk of the work

**Decision**: Treat the test migration as first-class scope, not cleanup. Every admin e2e spec
gains a navigation step; none of their assertions about *behaviour* should change.

**Finding**: `grep` over `e2e/tests/` shows nine of the ten specs touch the admin area:
`admin-access`, `admin-journey`, `date-poll-journey`, `export-and-backup`, `import`, `maintenance`,
`results-summary`, `storage-resilience`, `zero-signup`. Only `timezone.spec.ts` is untouched.

The controls they drive move as follows, and the `data-testid` values are deliberately **kept**:

| Test id | Was | Becomes |
|---------|-----|---------|
| `download-backup`, `maintenance-toggle` | `/admin` | `/admin/einstellungen` |
| `import-panel` and friends | `/admin`, always visible | `/admin/terminfindungen`, behind a reveal |
| `poll-form` and friends | `/admin`, always visible | `/admin/terminfindungen`, behind a reveal |
| `show-results` → grid | inline on `/admin` | `/admin/terminfindungen/:pollId` |
| `export-poll`, `delete-poll` | `/admin` | `/admin/terminfindungen` (unmoved within the area) |
| `maintenance-banner` | inside the switch | the shell, every area |

**Rationale for keeping the ids**: the migration should change *where a test navigates*, not *what
it asserts*. Renaming the ids at the same time would mean every diff mixes a move with a rename,
and a genuine behaviour regression could hide in the noise. Principle II's point is that the tests
define the behaviour; the behaviour is not changing, so the assertions should not either.

**Constitution gate 3** deserves explicit mention: `zero-signup.spec.ts` and
`date-poll-journey.spec.ts` prove a participant can answer from a bare link with no account. FR-009
and SC-010 say the shell must not touch that path at all, so those two specs should need **only**
whatever setup navigation their admin fixture does — if either needs a change to its participant
assertions, something is wrong with the implementation, not the test.

---

## Summary of what Phase 1 must carry forward

- The shell is a parent route with nested children (R-1), drawer per R-2, addresses per R-5.
- One dashboard endpoint, five fixed queries, maintenance excluded from the payload (R-3).
- **The measurement spike for figure 6 comes first, before the endpoint is designed into
  existence** (R-4). Its outcome may amend SC-011.
- Banner and switch move together in one task (R-6).
- Dashboard store carries loading / empty / unreachable / unauthorized (R-7).
- Answers page is a route with a not-found redirect to the list (R-8).
- Nine e2e specs gain navigation and keep their assertions and test ids (R-9).
