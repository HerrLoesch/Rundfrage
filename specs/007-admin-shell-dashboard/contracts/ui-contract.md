# UI Contract: Admin Shell, Dashboard and Settings

**Feature**: 007-admin-shell-dashboard
**Date**: 2026-09-12

What the interface consists of, which states each part has, and what the tests assert. Existing
`data-testid` values are **kept** wherever a control merely moves — R-9 explains why, and the
migration table there says what moves where.

---

## §1 The shell

`AdminShell.vue`, the parent route at `/admin`. Owns the app bar, the navigation drawer, the
maintenance banner and sign-out. Children render into a nested `<RouterView>`.

| Element | Test id | Contract |
|---------|---------|----------|
| Shell root | `admin-shell` | Present on every `/admin/*` address except `/admin/anmelden`. Absent on the participant surface and on the sign-in form (FR-008, FR-009). |
| Wordmark | `brand` | Already exists. Inside the shell it links to `/admin` — the dashboard (FR-036). |
| Drawer | `admin-nav` | See §2. |
| Drawer toggle | `admin-nav-toggle` | Rendered **only** below the `md` breakpoint (FR-012). |
| Sign-out | `sign-out` | Already exists. Moves into the shell; reachable from every area, never a nav entry (FR-010). |
| Maintenance banner | `maintenance-banner` | Already exists. Moves into the shell; see §6. |

**Persistence (FR-001)**: navigating between areas must not remount the drawer. The test is
behavioural rather than structural: open the drawer's scroll position or its temporary-open state,
navigate, and assert it is unchanged.

**Sign-in is outside** (FR-008). `/admin/anmelden` renders `SignInForm` with no shell around it, so
`admin-shell` and `admin-nav` are absent. Signing in navigates to `/admin` in every case, and the
refused address is never restored (FR-011a).

---

## §2 The navigation

`AdminNav.vue`. A `v-navigation-drawer` containing `v-list nav`; each entry is a `v-list-item` with
`:to`. `permanent` from `md` up, `temporary` below.

| Order | Label | Address | Test id |
|-------|-------|---------|---------|
| 1 | Dashboard | `/admin` | `nav-dashboard` |
| 2 | Terminfindungen | `/admin/terminfindungen` | `nav-polls` |
| 3 | Einstellungen | `/admin/einstellungen` | `nav-settings` |

**Contract**:

- Exactly three entries, in this order. Settings is last and nothing follows it (FR-003, FR-018).
- No entry for an unbuilt feature (FR-004).
- Labels come from the catalogue (FR-035). Entry 2 reuses `poll.listTitle`, the catalogue's existing
  word for the poll list, rather than introducing a second word for the same thing (FR-002a).
- **Current state is not ours to write.** Vue Router sets `aria-current="page"` on the active link.
  Tests assert `aria-current`, not a CSS class — that is what assistive technology reads, and a
  class could drift from it (R-2).
- Exactly one entry carries `aria-current` at a time (FR-002).
- While `/admin/terminfindungen/:pollId` is shown, entry 2 keeps `aria-current` (FR-014e).
- Keyboard: every entry is reachable and activatable by keyboard alone (FR-013, SC-008). `v-list
  nav` supplies the `<nav>` landmark.
- Below `md`: choosing an entry closes the drawer, so the content is not left covered (FR-012).

---

## §3 The dashboard — `/admin`

`DashboardView.vue`. Six labelled figures. Reports; holds no control (FR-033).

| Figure | Test id | Source |
|--------|---------|--------|
| Polls stored | `stat-polls` | `pollCount` |
| Answers total | `stat-responses` | `responseCount` |
| Polls unanswered | `stat-unanswered` | `unansweredPolls` |
| Deletions due soon | `stat-deletions` | `deletionsDueSoon` + `nextDeletion` |
| Maintenance | `stat-maintenance` | the `maintenance` store, **not** the payload |
| Yes / maybe / no | `stat-distribution` | `yes`, `maybe`, `no` |

Each figure states what it counts (FR-028). The container is `dashboard`.

### States

| State | Test id | Contract |
|-------|---------|----------|
| Loading | `dashboard-loading` | Placeholders. **No number is rendered**, not even a 0 — two seconds of zeros reads as data (R-7). |
| Unreachable | `storage-unavailable` | Existing wording. **No figure at all** (FR-032). |
| Nothing stored | `dashboard-empty` | Words, not zeros (FR-031). Shown when `pollCount === 0`. |
| Nothing answered | `distribution-empty` | Replaces the distribution when `yes + maybe + no === 0` while polls exist (FR-028 scenario 8). |
| Ready | — | The six figures. |

`unauthorized` is not a visual state: it redirects to sign-in (FR-011).

**Three readings that must not be conflated** in the deletions figure:

1. no poll exists — `nextDeletion` is `null`;
2. polls exist, none due within seven days — `deletionsDueSoon === 0`, `nextDeletion` set;
3. some are due — `deletionsDueSoon > 0`.

**Refresh (FR-030)**: the store re-reads on mount. Because the router does not keep areas alive,
returning to the dashboard remounts the view, so a change made elsewhere is reflected without a
manual reload and without a watcher.

---

## §4 The date-poll area — `/admin/terminfindungen`

`PollList.vue`, trimmed. Opens on the list (FR-014h).

| Element | Test id | Contract |
|---------|---------|----------|
| Create action | `poll-create-toggle` | Reveals `PollForm`. Not expanded on arrival. |
| Import action | `poll-import-toggle` | Reveals `ImportPanel`. Not expanded on arrival. |
| Create form | `poll-form` | Existing. Now revealed rather than permanent. |
| Import panel | `import-panel` | Existing. Now revealed rather than permanent. |
| List item | `poll-list-item` | Existing. Summaries only — no answers inline (FR-014b). |
| Open answers | `show-results` | Existing id, now a link to `/admin/terminfindungen/:pollId`. |
| Export | `export-poll` | Existing, stays on the list (FR-014d). |
| Delete | `delete-poll` | Existing, stays on the list (FR-014d). |
| Participant link | `poll-list-link` | Existing, unchanged. |
| Empty state | `poll-list-empty` | Existing. Must also offer creating directly (FR-014l). |
| Storage unavailable | `storage-unavailable` | Existing, unchanged (FR-017). |

**Contract**:

- At most one of the two forms is open at a time (FR-014i).
- Revealing a form changes nothing; closing it or leaving the area discards the entry and leaves no
  submission armed (FR-014j).
- A rejected form stays open with the entry intact and the reason shown (FR-014k).
- **No maintenance, backup or restore control appears here** (FR-015, SC-002). Worth an explicit
  assertion: the absence is the requirement.

### One poll's answers — `/admin/terminfindungen/:pollId`

`PollAnswersView.vue`.

| Element | Test id | Contract |
|---------|---------|----------|
| Container | `poll-answers` | Carries the poll's title and message (FR-014c). |
| Grid | existing `ResultGrid` ids | Per-day summary, best-day marking and per-response deletion, all unchanged (FR-016). |

**On pagination — a gap this feature found and closed.** `ResultsProjection` has paged
server-side at 50 since 002 (research R-7), the payload has always carried `page`/`pageCount`, and
004's UI contract lists "the paging control and its page size" among what it leaves unchanged. No
component ever rendered one: `results.page`, `results.previous` and `results.next` sat unused in
the catalogue, and a poll at 002's 1000-response limit showed 50 answers with no way to the other
950. Giving the answers their own address made that plain, so the control now exists:

| Element | Test id | Contract |
|---------|---------|----------|
| Container | `results-paging` | Rendered only when `pageCount > 1` — a control reading "1 of 1" is noise |
| Previous | `results-previous` | Disabled on the first page; emits `changePage` with `page - 1` |
| Position | `results-page` | "Seite {page} von {pageCount}", in an `aria-live="polite"` region, because paging replaces rows in place and a screen reader is otherwise told nothing |
| Next | `results-next` | Disabled on the last page; emits `changePage` with `page + 1` |

`ResultGrid` reports the intent and does not fetch — the same split `deleteResponse` already uses,
which is what lets one component serve the participant view and the creator's through different
endpoints (006 FR-004). All three surfaces page: the poll link, the creator's answers page, and
the personal link — the last needed a `page` parameter added to `GET /responses/{editToken}`,
which had hardcoded the first page. For a participant, paging leaves a half-written answer
untouched, which is asserted end to end.
| Back to list | `back-to-polls` | Returns to `/admin/terminfindungen` (FR-014c). |
| Not found | redirect | A `404` redirects to the list and says the poll is not there (FR-014g). |

- No export or delete-poll control here — deliberately not duplicated (FR-014d).
- Deleting the last answer leaves the operator here, showing the grid's empty state (FR-014f).
- The address works on its own: reloaded or pasted, it loads the same answers (FR-014a, SC-006).

---

## §5 Settings — `/admin/einstellungen`

`SettingsView.vue`. Three titled sections, no modal dialog (FR-020). Ordered by consequence, least
destructive first, restore last and visibly separated (FR-020a).

| Order | Section | Test id | Contents |
|-------|---------|---------|----------|
| 1 | Maintenance mode | `settings-maintenance` | `MaintenanceSwitch` — toggle only, no banner (§6) |
| 2 | Backup | `settings-backup` | `download-backup`, existing |
| 3 | Restore | `settings-restore` | `RestorePanel`, existing, visibly separated |

**Contract**:

- Each section carries its own name and description (FR-020).
- Reading the page changes nothing; every change keeps the confirmation it has today (FR-021).
- Maintenance keeps its asymmetry: on asks first, off does not (FR-023).
- Restore keeps its guard: refused unless maintenance is on, and it names the missing step
  (FR-024).
- Leaving with a step begun — a chosen file, a previewed restore, an unanswered prompt — changes
  nothing, and the step is **not** waiting on return (FR-022). Assert by beginning a restore,
  navigating away, returning, and finding no armed confirmation.
- **No poll control appears here** (SC-002), and import is not here — it is in §4 (FR-019, FR-025).

---

## §6 The maintenance banner

Rendered by the **shell**, from the `maintenance` store. `MaintenanceSwitch` no longer renders it
(R-6).

| Property | Contract |
|----------|----------|
| Test id | `maintenance-banner`, unchanged |
| Where | Above the content region, in **every** admin area (FR-026) |
| Content | Existing `maintenance.bannerTitle`, `bannerBody`, and `maintenance.since` |
| Depends on | Maintenance being on — and nothing else. Not on the area shown, not on the switch being rendered (FR-026a) |
| Clears | As soon as maintenance is switched off, from whichever area (FR-026b) |
| Never on | The sign-in form; the participant surface, which keeps its own `maintenance-notice` (FR-026c) |

**The assertion that matters**: switch maintenance on from settings, navigate to the dashboard and
to the poll list, and find the banner in both. That is the test the old arrangement could not pass,
and the reason the banner had to leave the switch.

---

## §7 What must not change

Asserted negatively, because these are the requirements a refactor is most likely to break
quietly.

| Requirement | Assertion |
|-------------|-----------|
| FR-009, SC-010 | A participant on `/u/:token` sees no `admin-shell`, no `admin-nav`, and no link into `/admin`. Steps from link to answer form stay zero. |
| Principle I | `zero-signup.spec.ts` and `date-poll-journey.spec.ts` pass with no change to their *participant* assertions. A needed change there means the implementation is wrong, not the test (R-9). |
| FR-016 | Every existing poll capability behaves identically — summary, best-day mark, per-response delete, pagination. |
| FR-035 | No literal string in any new component. `noLiteralStrings.spec.ts` already enforces this and covers the new files for free. |
| SC-002 | The poll area has zero installation-wide controls; settings has zero poll controls. |
| FR-034 | The dashboard renders no poll title, participant name or individual answer. |
