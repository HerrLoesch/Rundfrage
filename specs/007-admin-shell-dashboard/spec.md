# Feature Specification: Admin Shell, Dashboard and Settings

**Feature Branch**: `007-admin-shell-dashboard`
**Created**: 2026-09-11
**Status**: Draft
**Input**: User description: "Ich möchte dass die UI optimiert wird. Im Admin Bereich soll dafür alles in eigene Settings Dialoge verschoben werden, was man als Settings und Maintenance zählen kann. Darüber hinaus, soll in der Admin Sicht, die anderen Funktionen (aktuell nur die Temrinfindung) in eine eigene Ansicht verschoben werden soll. Dazu soll die gesamte UI in Form eines Admin Stiles aufgebaut sein. Also links in einer navigations Leiste die Links zu verschiedenen Unterfeatures. Wobei die Startseite mit einem Dashboard startet, wo man diverse Statistiken sehen kann. Die Settings sollten immer die letzte Seite im Navigationsbereich sein."

## Summary

Six features have each added their controls to the same admin page. It now carries, from top to
bottom and in one column: a backup download, a maintenance switch, a sign-out, a poll creation
form, a poll import panel, a whole-system restore panel, and then the list of polls with their
results, exports and deletions. Everything an operator can do is on screen at once, and the page
gives no clue which of those things are done weekly, which are done once a year, and which destroy
all data.

This feature stops adding to that page and gives the admin area a shape instead. The operator
works inside a shell: a navigation bar on the left listing the areas of the system, a content area
to its right showing one area at a time. The first entry is a **dashboard** that answers *"what is
in here right now?"* without the operator opening anything. The middle entries are the system's
actual capabilities — today exactly one, date polls. The **last entry is always settings**, and it
holds everything that configures or maintains the installation rather than serving a poll.

Three properties are worth stating because they are the point of the change, not side effects:

**Frequency decides placement.** Creating a poll and reading its answers happen constantly; they
get a page of their own with nothing else on it. Switching on maintenance mode and restoring a
backup happen rarely and are dangerous when they happen by accident; they move onto a settings page
the operator has to go to. The operator can no longer reach *replace every poll in the system* by
scrolling past the poll they came to look at.

**The navigation is the list of features.** "Terminfindung" stops being *the* admin page and
becomes *an* entry. Whatever Rundfrage grows next appears beside it without anyone deciding where
on a long page it should go, and settings stays where it is — at the bottom.

**Nothing changes for participants.** Principle I says a poll link leads to a poll and nothing
else. The shell, its navigation and its dashboard live entirely behind the operator session; the
participant surface keeps the bare app bar it has today and gains no navigation towards an area it
cannot use.

## Clarifications

### Session 2026-09-11

- Q: Which of today's controls count as "Settings and Maintenance" and move behind the settings
  entry? → A: **Maintenance mode, backup download and backup restore. Poll import stays with the
  date polls.** Import reads an exported file and produces a poll — it serves a poll, so it belongs
  to the area that serves polls. The alternative would have seated *add one poll* beside *replace
  every poll*, which is the adjacency 005 FR-001 was written to prevent; this split enforces that
  requirement more strongly than 005 did, by putting the two in different navigation areas
  entirely. Sign-out is shell chrome and is not a settings item (FR-010).
- Q: How does the settings area present its items — as dialogs opened from the settings page, or as
  sections laid out on it? → A: **As titled sections of the settings page. No modal dialogs.** The
  request names both a "Dialog" and a "Seite"; the page wins because the project already decided
  this once. `MaintenanceSwitch` carries an inline confirmation rather than a modal on the stated
  grounds that the operator is already looking at the control, and a second pattern for the same
  kind of decision would contradict it. Accepted consequence: the settings items are all on screen
  together, which is the arrangement this feature removed from the poll page. It is tolerable here
  and only here, because settings holds three items rather than seven, and because none of them is
  something the operator scrolled past on the way to another task.
- Q: Which statistics does the dashboard show? → A: **Six figures: total polls; total answers;
  polls with no answers yet; polls being deleted within the next seven days, with the next deletion
  date; the maintenance-mode state; and the overall distribution of *yes*, *maybe* and *no* across
  every poll.** The first five answer "what is in here, and does anything need me?" and come
  straight off the poll list the admin area already reads. The sixth is different in kind and was
  chosen deliberately: it is the only figure that says anything about how people *answered* rather
  than how much was stored, and it is the one an operator cannot assemble by looking at any single
  poll. Accepted consequence: it cannot be derived from the poll list, so obtaining it means
  aggregating across every poll's answers — a cost the other five do not carry, bounded by SC-011.
  Nothing new is recorded to make it possible; it is a sum over answers that already exist.
- Q: Do a poll's answers stay an inline expansion of the list, or become their own destination? →
  A: **Their own destination, with its own address.** Today the results grid unfolds inside the
  poll card and lives in component state, so the largest screen in the admin area is the only one
  that cannot be linked to or survive a reload — an inconsistency the shell makes obvious, because
  every other area gains an address in FR-005. The grid also earns it: it carries pagination, the
  per-day summary, the best-day marking and per-response deletion, which is a destination rather
  than a disclosure. Accepted consequence: the poll list stops showing answers and shows summaries
  only. No task gains a step — opening answers is one action from the list either way — and the
  list's export and delete stay on the list rather than moving to the new page, which would have
  added one.
- Q: Do the poll creation form and the import panel stay permanently expanded above the list? →
  A: **No — both are revealed on demand; the list is what the area opens with.** Two always-open
  forms above the polls are the other half of why today's page feels crowded, and moving the
  maintenance controls out would have left them as the first two things an operator scrolls past.
  Accepted consequence, stated plainly because it is a real cost: creating a poll gains exactly one
  action, from zero to one, and SC-005 carries that exception rather than pretending it away. It is
  accepted because the action that gains the step is the one an operator arrives intending to take,
  while the list they must currently scroll past is what they see on every other visit. The empty
  state is the exception to the exception: with no polls stored there is nothing to show, so it
  offers creating directly.

### Session 2026-09-12

- Q: What scale must the dashboard's figures hold at? SC-011 referred to "the largest number of
  polls and answers the system documents as supported", and no maximum number of polls has ever
  been documented. → A: **500 polls, each at feature 002's limits of 1000 answers across 100
  candidate days.** SC-011's two seconds is now verifiable, and the number is deliberately large
  enough — 500,000 answers — that FR-028.6 cannot be assembled by reading each poll in turn, which
  settles the design pressure without the spec prescribing a mechanism. Two consequences are
  accepted rather than hidden. First, 500 is a *documented supported scale*, not a new enforced
  limit: nothing refuses the 501st poll, because enforcing a poll count would amend feature 002's
  FR-015 limit table and change poll creation, which this feature has no business doing. Second,
  past 500 polls the two-second promise lapses. The figures stay correct; only the timing claim
  stops applying.
- Q: How is maintenance mode's state made visible from every admin area, now that the switch has
  moved to settings? → A: **A persistent banner in the shell, above the content region, shown in
  every admin area while maintenance is on.** Today that banner is rendered by the switch itself
  inside the poll list, so moving the switch to settings would take the warning with it — and the
  documented failure mode of feature 005 is precisely forgetting to switch maintenance back off.
  Promoting the banner to the shell keeps its existing wording and its "since" timestamp, makes it
  unmissable wherever the operator is, and decouples it from the control, which is what allows the
  control to move at all. Accepted consequence: the banner costs vertical space in every admin area
  for as long as maintenance is on. That is the intended pressure — the state is meant to feel
  temporary.
- Q: What are the navigation entries called? → A: **"Dashboard", "Terminfindungen",
  "Einstellungen".** "Terminfindungen" is already the catalogue's word for the poll list
  (`poll.listTitle`) and is reused rather than reinvented. "Dashboard" is a deliberate exception to
  a pattern and is recorded as one: every other string in the catalogue is German even where a
  loanword was available — `Sicherung` rather than "Backup", `Wartungsmodus` rather than
  "Maintenance" — so an English noun in the navigation stands out. It is chosen anyway because it
  is the word the feature was requested in, and because it names a kind of page that operators
  recognise across tools. Anyone later making the catalogue uniformly German should change this one
  knowingly, not as a tidy-up.
- Q: After signing in — including after a session expired mid-task — where does the operator land?
  → A: **Always the dashboard, whatever they were doing.** One rule, and it is the one the code
  already follows unconditionally. Returning to the refused address would recover the interrupted
  task more gracefully, but it means carrying an intended destination across an authentication
  boundary, and Principle III asks for the second concrete use before that machinery exists.
  Accepted consequence: an operator whose session expires while reading one poll's answers pays one
  navigation click to get back, and the address is still in their history.

### Session 2026-09-12 (second entry — amendment on measurement)

- Q: Does SC-011's two-second budget hold at the scale FR-028c documented? → A: **No, and both were
  amended.** The scale and the budget were written in the same clarification session without being
  costed against each other. Measurement (T001–T003) found the aggregate linear in the number of
  day-answer rows at about 0.27 s and 219 MiB per million, so FR-028c's stated worst case of
  50,000,000 rows extrapolates to roughly 14 seconds and 10.7 GB on disk — against a two-second
  budget.

  The design was not the problem and did not change. The honest fix was the criterion: FR-028c now
  bounds the supported scale at 500 polls holding up to 2,500,000 day-answers in total, stated in
  day-answers because that is what the cost actually tracks, and SC-011 is measured against it
  (0.66 s, roughly threefold margin).

  The decisive finding was not the timing but the storage. An 11 GB SQLite file contradicts the
  constitution's own rationale for choosing SQLite — PostgreSQL was rejected because its image and
  resident server outweighed data "of which the actual polls are a few kilobytes". The original
  scale was arithmetically possible and practically meaningless.

  Rejected again, now on measurement rather than principle: making figure 6 fast. At 0.27 s per
  million the only way to beat a full scan is to avoid it, which requires a maintained total —
  refused by Principle IV and by the staleness it would carry across five deletion paths.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One area at a time, reached from the left (Priority: P1)

The operator signs in and lands inside a shell. On the left is a navigation bar listing the areas
of the admin system; the content to its right shows whichever one is selected. Date polls have
their own area, and it opens on the list of polls: creating one and importing one are actions that
reveal their form when asked for, a single poll's answers live on a page of their own, and
exporting and deleting stay one action away on the list. Settings has its own area, last in the list, containing what
configures and maintains the installation. Neither area shows the other's controls.

**Why this priority**: This is the restructure itself. Without it the other two stories have
nowhere to live: a dashboard is a navigation destination, and a settings area is a navigation
entry. It also delivers the whole point on its own — the poll page stops carrying controls that
replace the database.

**Independent Test**: Sign in, confirm a left navigation bar is present with settings as its last
entry, open the date-poll area and confirm it offers every poll task and no maintenance control,
open one poll's answers and confirm that address reloads to the same answers, then open the
settings area and confirm the maintenance and backup controls are there instead.

**Acceptance Scenarios**:

1. **Given** a signed-in operator, **When** the admin area loads, **Then** a navigation bar is
   shown on the left listing the available areas, and exactly one area is shown as the current one.
2. **Given** the navigation bar, **When** its entries are read in order, **Then** settings is the
   last entry, and no entry appears after it.
3. **Given** the operator is in the date-poll area, **When** the area is inspected, **Then** it
   offers creating a poll, importing a poll, listing polls, opening a poll's answers, exporting a
   poll and deleting a poll, and offers no maintenance-mode, backup or restore control.
4. **Given** the poll list, **When** the operator opens a poll's answers, **Then** the answers are
   shown at their own address, the navigation bar stays in place with the date-poll area still
   marked current, and a way back to the list is offered.
5. **Given** the address of a poll's answers, **When** it is opened directly or reloaded, **Then**
   the same answers are shown without going through the list first.
6. **Given** the operator is in the date-poll area, **When** they select the settings entry,
   **Then** the settings area replaces the content and the navigation bar stays in place with
   settings marked as current.
7. **Given** the operator is in any admin area, **When** the page is reloaded, **Then** the same
   area is shown again rather than the start page.
8. **Given** a participant opens a poll link, **When** the page loads, **Then** no navigation bar
   and no admin area is shown or linked.
9. **Given** a visitor who is not signed in, **When** they open any admin address, **Then** they
   reach the sign-in form, which shows no navigation bar.

---

### User Story 2 - Settings and maintenance, on a page of their own (Priority: P2)

Everything that configures or maintains the installation — rather than serving a single poll —
lives on the settings page: maintenance mode, the backup download, and restoring a backup, in that
order, restore last and set apart. The operator who came to switch on maintenance mode goes to
settings and switches it on. The operator who came to restore a backup goes to settings, reaches
the section at the bottom, and is told there what it will replace. Nobody reaches either while on
their way to a poll.

**Why this priority**: These are the rarest and most destructive actions in the system, and today
they sit between the poll form and the poll list. Separating them is the safety half of this
feature. It ranks below the shell only because it needs the settings entry to exist first.

**Independent Test**: From the settings page, switch maintenance mode on and off, download a
backup, and run a restore through its preview and confirmation — all without leaving the page, and
with none of those three controls reachable from the date-poll area.

**Acceptance Scenarios**:

1. **Given** the settings area, **When** it is opened, **Then** maintenance mode, backup download
   and backup restore are each shown as a titled section with its own description, restore is last
   and visibly separated, and no modal dialog is used.
2. **Given** the settings area, **When** the operator switches maintenance mode on, **Then** the
   confirmation and the resulting state are shown exactly as they are today, and the state remains
   visible from every admin area, not only from settings.
3. **Given** maintenance mode is off, **When** the operator opens the restore item, **Then** it
   refuses to run and names the missing step, as it does today.
4. **Given** a settings section with a step begun — a chosen file, a previewed restore — **When**
   the operator leaves the settings area without confirming, **Then** nothing in the system has
   changed.
5. **Given** the operator left settings mid-step, **When** they return to settings, **Then** the
   half-completed step is not waiting for them and no confirmation is one click from completing.

---

### User Story 3 - A dashboard that answers "what is in here?" (Priority: P3)

The first entry in the navigation is a dashboard, and it is where signing in leads. It shows the
state of the installation in figures — how much is stored, how much is waiting for answers, what is
about to be deleted, whether maintenance mode is on, and how the answers fall across *yes*, *maybe*
and *no* — so that the operator knows whether anything needs doing before opening anything.

**Why this priority**: It is the most visible part of the request and the least load-bearing. The
admin area is usable and safer with US1 and US2 alone; the dashboard turns it from a set of pages
into a starting point. It is last because it reports on the other areas and therefore benefits from
them existing first.

**Independent Test**: Sign in with a known set of polls and answers and confirm the landing page
reports figures that match them — including the *yes*/*maybe*/*no* totals, checked against the sum
of the per-poll summaries; then delete a poll and confirm every affected figure follows.

**Acceptance Scenarios**:

1. **Given** a signed-in operator, **When** they land in the admin area without choosing an entry,
   **Then** the dashboard is shown and is marked as the current navigation entry.
2. **Given** stored polls and answers, **When** the dashboard is shown, **Then** each statistic it
   presents is labelled and matches what the corresponding area would show.
3. **Given** several polls with answers, **When** the dashboard's *yes*/*maybe*/*no* totals are
   compared against the per-day summaries of every poll added together, **Then** the two agree.
4. **Given** the operator changes something that a statistic counts, **When** they return to the
   dashboard, **Then** the statistic reflects the change.
5. **Given** no polls exist at all, **When** the dashboard is shown, **Then** it says so in words
   and does not present an empty or zeroed layout that could be mistaken for a failure.
6. **Given** the stored data cannot be read, **When** the dashboard is shown, **Then** it says the
   data is unreachable and does not show zeros.
7. **Given** maintenance mode is on, **When** the dashboard is shown, **Then** that is among what
   it reports, together with since when.
8. **Given** polls exist but none has been answered, **When** the dashboard is shown, **Then** the
   *yes*/*maybe*/*no* distribution says there is nothing to distribute rather than showing three
   zeros.

---

### Edge Cases

- **The session expires while an area is open.** Any admin area whose data the server refuses as
  unauthorised must lead to the sign-in form, exactly as the poll list does today — the server
  remains the authority on the session, and the navigation bar must not imply access the server
  will deny. Signing in again returns the operator to the dashboard, not to what was interrupted
  (FR-011a); the interrupted address is left in their history rather than restored for them.
- **Storage is unreachable.** Every area must distinguish *"there is nothing"* from *"this cannot
  be read right now"*. This is already required of the poll list; the dashboard makes it more
  dangerous, because a statistic reading zero is a claim about the data rather than an empty list.
- **A narrow screen.** The navigation bar cannot occupy a phone-width screen permanently. It must
  remain reachable and must not cover the content it navigates to once a destination is chosen.
- **An unknown admin address.** An address under the admin area that matches no navigation entry
  must land somewhere defined rather than showing an empty shell.
- **A poll that is gone.** An answers address whose poll was deleted — in another tab, or by the
  retention deadline passing — must land on the poll list and say so, not present an empty grid
  that reads as "nobody answered".
- **Maintenance mode is on and the operator is not in settings.** The switch lives in settings, but
  the warning belongs to the shell and must be on screen in every area — the documented failure
  mode of that feature is forgetting to switch it back off. A warning that travelled with the
  switch would be visible only where the operator no longer needs it.
- **A form is abandoned half-way.** Opening the creation or import form and leaving the area must
  discard the entry rather than restore it on return, so that nobody submits a poll they had
  forgotten they half-wrote.
- **A destructive step is abandoned half-way.** Choosing a backup file and previewing a restore
  must change nothing by itself, and navigating away must discard the step rather than leave a
  confirmation armed for whoever opens settings next.
- **One navigation entry only.** Should the middle section of the navigation ever hold a single
  entry, the navigation must still read as a list of areas rather than collapsing into the page it
  points at.

## Requirements *(mandatory)*

### Functional Requirements

#### The shell and its navigation

- **FR-001**: The admin area MUST be presented as a shell consisting of a navigation bar on the
  left and a content region beside it, with the navigation bar persisting unchanged while the
  content region changes.
- **FR-002**: The navigation bar MUST list the admin areas as links, one entry per area, and MUST
  mark exactly one entry as the area currently shown.
- **FR-002a**: The entries MUST be labelled, in order, "Dashboard", "Terminfindungen" and
  "Einstellungen". Each label MUST come from the translation catalogue (FR-035), and
  "Terminfindungen" MUST reuse the catalogue's existing term for the poll list rather than
  introduce a second word for the same thing.
- **FR-003**: The navigation bar MUST order its entries as: the dashboard first, the feature areas
  in the middle, and settings last. No entry may appear after settings.
- **FR-004**: The navigation bar MUST list only areas that exist. Entries for planned or unbuilt
  features MUST NOT be shown.
- **FR-005**: Each admin area MUST have its own address, so that it can be reloaded, bookmarked and
  returned to directly.
- **FR-006**: Entering the admin area without naming an area MUST lead to the dashboard.
- **FR-007**: An address under the admin area that matches no entry MUST lead to the dashboard
  rather than to an empty shell or an error.
- **FR-008**: The shell, its navigation bar and every area it contains MUST be shown only to a
  signed-in operator. The sign-in form MUST NOT show the navigation bar.
- **FR-009**: The participant surface MUST NOT show the shell, the navigation bar, or any link into
  the admin area.
- **FR-010**: Signing out MUST be reachable from the shell itself, from every admin area, and MUST
  NOT be an entry in the list of areas.
- **FR-011**: When the server refuses an admin request as unauthorised, the operator MUST be taken
  to the sign-in form regardless of which area they were in.
- **FR-011a**: Signing in successfully MUST lead to the dashboard, in every case. The area or
  destination the operator was refused from MUST NOT be remembered, restored, or carried through
  the sign-in form.
- **FR-012**: On screens too narrow to show the navigation bar beside the content, the navigation
  MUST remain reachable through a control in the app bar, and choosing an entry MUST reveal the
  content rather than leave it covered.
- **FR-013**: The navigation MUST be operable by keyboard alone, and each entry MUST expose its
  name and its current/not-current state to assistive technology.

#### The date-poll area

- **FR-014**: Date polls MUST have their own area containing creating a poll, importing a poll from
  an exported file, listing polls, opening a poll's answers, exporting a poll and deleting a poll.
- **FR-014a**: A poll's answers MUST be their own destination with its own address, reachable in one
  action from the poll list and reloadable and linkable like any other admin address (FR-005).
- **FR-014b**: The poll list MUST show per-poll summaries only — title, day count, answer count,
  deletion date and the participant link — and MUST NOT render any poll's answers inline.
- **FR-014c**: The answers destination MUST carry the poll's title and message, the answers grid
  with its pagination, the per-day summary with its best-day marking, and per-response deletion. It
  MUST offer a way back to the poll list.
- **FR-014d**: Exporting a poll and deleting a poll MUST stay on the poll list, reachable in one
  action from it, so that no existing task gains a step. They MUST NOT also be duplicated onto the
  answers destination.
- **FR-014e**: While the answers destination is shown, the navigation bar MUST stay in place and
  MUST mark the date-poll area as current.
- **FR-014f**: Deleting the last remaining answer MUST leave the operator on the answers
  destination showing its empty state, not return them to the list.
- **FR-014g**: An answers address naming a poll that does not exist — deleted elsewhere, or never
  present — MUST lead to the poll list and say the poll is not there, rather than to the dashboard
  or an empty grid.
- **FR-014h**: The date-poll area MUST open showing the poll list. Creating a poll and importing a
  poll MUST each be an action that reveals its form when chosen, and neither form may be expanded
  before it is asked for.
- **FR-014i**: The creating and importing actions MUST be separately named and separately revealed,
  and at most one of their forms may be open at a time.
- **FR-014j**: Revealing a form MUST change nothing. Closing it, or leaving the area, MUST discard
  what was entered rather than leave it waiting, and MUST NOT leave a submission armed.
- **FR-014k**: A form that rejects what was entered MUST stay open with the entry intact and the
  reason shown, as it does today.
- **FR-014l**: When no polls are stored at all, the area MUST say so and offer creating a poll from
  that empty state directly, without the operator first finding the action.
- **FR-015**: The date-poll area MUST NOT contain any control that configures or maintains the
  installation as a whole.
- **FR-016**: Every date-poll capability that exists today MUST remain available with unchanged
  behaviour, including the per-day summary, the best-day marking, the per-response deletion and the
  pagination of answers.
- **FR-017**: The date-poll area MUST continue to distinguish "no polls have been created" from
  "the stored data cannot be read right now", in the words it uses today.

#### The settings area

- **FR-018**: Settings MUST be the last entry in the navigation bar and MUST contain everything
  that configures or maintains the installation rather than serving an individual poll.
- **FR-019**: The settings area MUST contain exactly these items: switching maintenance mode,
  downloading a backup, and restoring a backup. It MUST NOT contain poll import, which stays in the
  date-poll area (FR-014), and MUST NOT contain sign-out, which is shell chrome (FR-010).
- **FR-020**: Each settings item MUST be a titled section of the settings page, carrying its own
  name and its own description of what it does. No settings item may be presented as a modal
  dialog.
- **FR-020a**: The settings page MUST order its sections by consequence, least destructive first,
  and MUST place restoring a backup last with a visible separation from the sections above it.
- **FR-021**: Reading the settings page MUST change nothing by itself. Every change MUST require
  the same explicit confirmation it requires today, stated in the section that makes it.
- **FR-022**: Leaving the settings area with a step begun but unconfirmed — a chosen backup file, a
  previewed restore, an unanswered maintenance prompt — MUST leave the system unchanged, and that
  half-completed step MUST NOT be carried into another area or be waiting when settings is
  reopened.
- **FR-023**: Maintenance mode MUST keep its current asymmetry: switching it on asks first,
  switching it off does not.
- **FR-024**: Restoring a backup MUST keep its current guard — it is refused unless maintenance
  mode is on, and it names the missing step when refused.
- **FR-025**: Importing one poll and restoring a whole backup MUST remain in different navigation
  areas, so that adding one poll and replacing every poll cannot be reached by the same reflex
  (005 FR-001). Within settings, the restore item MUST remain visually distinct from the backup
  download it sits beside.
- **FR-026**: While maintenance mode is on, the shell MUST show a banner above the content region
  in every admin area, carrying the existing warning wording and the moment maintenance was
  switched on.
- **FR-026a**: The banner's presence MUST depend only on maintenance being on, not on which area
  is shown and not on the switch control being rendered. Moving or removing the switch MUST NOT be
  able to remove the warning.
- **FR-026b**: The banner MUST disappear as soon as maintenance is switched off, from whichever
  area the operator is in.
- **FR-026c**: The banner belongs to the admin shell only. It MUST NOT appear on the sign-in form,
  and the participant surface MUST keep the separate maintenance notice it has today rather than
  the operator's banner.

#### The dashboard

- **FR-027**: The dashboard MUST be the first navigation entry and the area the operator lands in.
- **FR-028**: The dashboard MUST present these figures, each labelled with what it counts:
  1. the total number of polls stored;
  2. the total number of answers across all polls;
  3. the number of polls that have received no answer yet;
  4. the number of polls whose deletion date falls within the next seven days, together with the
     earliest such date;
  5. whether maintenance mode is on, and since when if it is;
  6. the overall counts of *yes*, *maybe* and *no* across every answer in the system.
- **FR-028a**: The distribution in FR-028.6 MUST be an aggregate over all polls. It MUST agree with
  the sum of the per-day summaries a reader would find by opening every poll, and a poll with no
  answers MUST contribute nothing to it rather than contributing zeros that alter the reading.
- **FR-028b**: The dashboard's figures MUST NOT be obtained by reading each poll's answers in turn.
  At the supported scale of FR-028c that is 500 separate reads of up to 1000 answers each, which
  SC-011 cannot survive.
- **FR-028c**: The supported scale for SC-011 is **500 polls holding up to 2,500,000 day-answers in
  total** — for example 500 polls averaging 100 responses across 50 candidate days. The bound is
  stated in total day-answers rather than as a product of per-poll maxima because that is the
  quantity the cost depends on: measurement showed the aggregate is linear in the number of
  day-answer rows, at about 0.27 s per million (research.md R-4).

  This is a documented scale, not an enforced limit: no new rule may refuse the creation or import
  of a poll on the grounds of how many already exist, or how many answers are stored, and feature
  002's limit table is not amended.

  Beyond this scale the figures MUST remain correct; only SC-011's timing claim ceases to apply. At
  feature 002's per-poll maxima applied to all 500 polls — 50,000,000 day-answers — the aggregate
  measures about 14 seconds and the database about 10.7 GB. Both numbers are recorded to make the
  lapse explicit rather than surprising.
- **FR-029**: Every figure the dashboard shows MUST agree with what the corresponding area shows
  for the same data.
- **FR-030**: The dashboard MUST re-read its figures when the operator returns to it, so that a
  change made elsewhere in the admin area is reflected without a manual reload.
- **FR-031**: When no data exists at all, the dashboard MUST say so in words rather than presenting
  zeros.
- **FR-032**: When the stored data cannot be read, the dashboard MUST say the data is unreachable
  and MUST NOT present any figure, so that a reading of zero is never a guess.
- **FR-033**: The dashboard reports; it does not act. It MUST NOT hold a control that exists
  nowhere else, and no task may require passing through it. Its figures MAY be sums that no other
  area presents in that form — the *yes*/*maybe*/*no* distribution is one — provided each is
  derived from data another area also shows (FR-029, FR-028a).
- **FR-034**: Dashboard figures MUST be counts and states over data the system already stores. No
  participant name, no individual answer, no poll title and no access record may appear on the
  dashboard. The distribution in FR-028.6 is an aggregate and names nobody; presenting it MUST NOT
  require recording anything that is not recorded today.

#### Presentation

- **FR-035**: All text introduced by this feature MUST come from the translation catalogue, with no
  literal strings in the interface.
- **FR-036**: The shell MUST keep the application wordmark in the app bar, and the wordmark MUST
  lead to the dashboard while signed in.

### Key Entities

- **Admin area**: A named destination in the admin shell with its own address and its own content.
  Has a position in the navigation order — first (dashboard), middle (feature areas), or last
  (settings).
- **Navigation entry**: The link representing one admin area, carrying a name, a position, and
  whether it is the area currently shown. A destination inside an area — a single poll's answers —
  has its own address but no entry of its own; the area it belongs to stays marked as current.
- **Dashboard statistic**: One labelled figure or state derived from stored data, with a defined
  reading for "nothing stored" and a defined reading for "cannot be read".
- **Settings item**: One named, separately opened unit of configuration or maintenance, with its
  own confirmation where it changes something.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From landing in the admin area, an operator reaches any area in exactly one action.
- **SC-002**: The area an operator works in shows only controls belonging to that area: the
  date-poll area shows zero installation-wide controls, and the settings area shows zero poll
  controls.
- **SC-003**: No operation that destroys or replaces stored data is reachable from the date-poll
  area at all. Reaching one requires deliberately leaving that area for settings, where today it
  requires only scrolling.
- **SC-004**: An operator new to the installation can name what the system currently holds within
  10 seconds of signing in, without opening any area.
- **SC-005**: Every capability available before this change is still available after it. Exactly
  one task gains a step — creating a poll, which moves from zero actions to one because its form is
  no longer permanently open (FR-014h), and which costs nothing from the empty state (FR-014l).
  Every other date-poll task keeps its current number of actions, including opening a poll's
  answers, exporting a poll and deleting a poll.
- **SC-006**: Every admin address can be reloaded and returns the operator to the same place,
  including the address of a single poll's answers.
- **SC-007**: On a screen 375 pixels wide, every admin area is usable and no content is permanently
  covered by the navigation.
- **SC-008**: The whole admin area is operable by keyboard alone, and every navigation entry
  announces its name and whether it is current.
- **SC-009**: In every area, "nothing is stored" and "the data cannot be read" produce visibly
  different results, and neither is ever shown as a zero.
- **SC-010**: A participant reaching a poll link sees no navigation and no route into the admin
  area, and the number of steps between the link and the answer form remains zero.
- **SC-011**: At the scale FR-028c documents — 500 polls holding up to 2,500,000 day-answers in
  total — the dashboard has all six figures on screen within two seconds of being opened. Measured
  at 0.66 s for the aggregate on the development machine, leaving roughly a threefold margin for
  slower self-hosted hardware (research.md R-4).
- **SC-012**: With maintenance mode on, the warning is on screen in every admin area without
  scrolling, and switching it off from settings clears it everywhere.

## Assumptions

- **"Die gesamte UI" means the whole admin UI.** The participant surface is deliberately excluded:
  Principle I forbids putting anything between a link and an answer form, and navigation towards an
  area a participant cannot enter is exactly that. The sign-in form is excluded for the same reason
  — there is no session yet, so there are no areas to list.
- **The shell is chrome, not a feature area.** Sign-out and the wordmark belong to the shell and are
  reachable from every area; they are not entries in the navigation list.
- **The navigation lists one feature area today.** Date polls are the only capability the system
  has. The navigation is built to hold more, but no placeholder entry for an unbuilt feature is
  shown — Principle III rejects speculative structure.
- **No new data is collected for the dashboard.** Its figures are counts and states over what the
  system already stores. Principle IV forbids recording anything extra to make a statistic
  possible, and no access log, visit count or participant record is introduced. The
  *yes*/*maybe*/*no* distribution is a sum over answers that already exist; how that sum is
  obtained is a planning question, but two solutions are ruled out here — reading each poll in turn
  (FR-028b) and recording a running total that the deletion paths could leave stale (Principle IV
  forbids the extra record, and staleness would break FR-029).
- **500 polls is a scale, not a limit.** The project documents per-poll limits but has never
  documented how many polls an installation holds. This feature names 500 so that SC-011 can be
  measured, and deliberately stops there: no control refuses a poll because of how many exist.
- **Existing behaviour is preserved, not redesigned.** Poll creation, results, exports, import,
  maintenance mode and restore keep the behaviour features 002–006 specified. This feature moves
  them and changes where they are reached, not what they do.
- **Statistics are read on entering the dashboard.** No live updating, polling or push is assumed;
  returning to the dashboard is what refreshes it.
- **German remains the only interface language.** This feature adds navigation and dashboard labels
  to the existing catalogue and introduces no language selection. "Dashboard" is the single English
  noun among them, accepted knowingly (see Clarifications) rather than by drift.

## Dependencies

- Feature 002 (date poll) supplies every capability of the date-poll area.
- Feature 003 (SQLite and export) supplies the per-poll export and the backup download.
- Feature 004 (day summary and links) and feature 005 (import and maintenance mode) supply
  behaviour that must survive the move unchanged, in particular the import/restore separation
  (005 FR-001) and the maintenance-mode guard on restore (005 FR-024).
- Feature 006 (highlight best days) lives inside the results grid and moves with it.

## Out of Scope

- Any change to the participant answer flow, the poll link, or the edit link.
- New survey types beyond date polls. The navigation is shaped to hold them; none is built here.
- Configurable settings that do not exist today — themes, languages, retention periods, operator
  accounts.
- Historical or time-series statistics. The dashboard reports the current state, not a trend.
- Any change to the HTTP API's existing operations, beyond what reading the dashboard's figures
  requires.
- Enforcing a maximum number of polls. FR-028c documents 500 as a supported scale for measuring
  SC-011; adding a rule that refuses polls beyond it would amend feature 002's FR-015 limit table
  and belongs to its own feature.
