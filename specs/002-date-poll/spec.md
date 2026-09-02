# Feature Specification: Date Poll (Terminfindung)

**Feature Branch**: `dev` (feature directory `002-date-poll`; no feature branch per constitution v1.1.1)
**Created**: 2026-09-02
**Status**: Draft
**Input**: User description: "Es soll eine Adminoberfläche geben. In dieser soll man eine Terminfindung ähnlich zu doodle anlegen können. Dabei ist ein Titel zu vergeben, eine kurze Nachricht und dann soll man Tage auswählen können, für die die Nutzer später angeben können ob sie an dem Tag Zeit haben, keine Zeit haben oder evtl. Zeit haben."

## Overview

This is Rundfrage's first real product feature: a Doodle-style date poll. A creator signs in
to a protected admin area, gives a poll a title, a short message, and a set of candidate days.
Everyone else opens a link and marks, for each day, whether they have time, do not have time,
or might have time — **without creating an account, logging in, or installing anything**.

Constitution Principle I (Zero-Signup Participation) has been trivially satisfied until now
because no participant-facing flow existed. This feature is where it becomes binding, and
where its consequences are felt: response editing and duplicate handling must be solved with
link-scoped secrets rather than by identifying the participant.

## Clarifications

### Session 2026-09-02

- Q: What concrete limits should FR-015 enforce? → A: Title 300, message 2000, display name 100 characters; 100 candidate days; 1000 responses per poll
- Q: Session lifetime (FR-006) and failed-sign-in policy (FR-005)? → A: 8 hours of inactivity; 5 failed attempts then a 15-minute lockout
- Q: How should the system react to response spam? → A: Transient per-source rate limit (10/hour) plus creator deletion of individual responses
- Q: Which events should be logged? → A: Security-relevant events identified by technical id only — never names, answers or tokens
- Q: What accessibility baseline applies? → A: Four testable rules — keyboard operable, labelled controls, visible focus, states never distinguished by colour alone
- Q: Which time zone defines a calendar day (FR-011, FR-014, FR-039)? → A: One fixed zone, Europe/Berlin, for every day boundary and the retention deadline
- Q: How is *no answer* handled in the grid and the per-day totals? → A: Not counted; the cell stays empty, and the three counts cover only answered states
- Q: How does the 30-day retention take effect (FR-039, FR-040)? → A: Access refused the moment the deadline passes; a background job erases the data at least daily

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A creator signs in and creates a date poll (Priority: P1)

A creator opens the admin area, is asked to authenticate, and after signing in creates a poll:
a title, a short message, and a selection of candidate days. On saving, the system shows a
participant link that can be copied and shared.

**Why this priority**: Nothing else can exist without a poll. It is the smallest slice that
produces something real and demonstrable.

**Independent Test**: Sign in, create a poll with a title, a message and three days, and
confirm a participant link is shown. Requires no other story.

**Acceptance Scenarios**:

1. **Given** an unauthenticated visitor, **When** they open the admin area, **Then** they are
   asked to authenticate and see no poll data of any kind.
2. **Given** a signed-in creator, **When** they submit a title, a message and at least one day,
   **Then** the poll is stored and a participant link is displayed.
3. **Given** a signed-in creator, **When** they submit without a title or without any day,
   **Then** the poll is not created and the missing input is named.
4. **Given** a creator selects the same day twice, **When** they save, **Then** the day appears
   exactly once.
5. **Given** a created poll, **When** the creator returns to the admin area later, **Then** the
   poll is listed with its participant link.

---

### User Story 2 - Anyone answers via the link, without an account (Priority: P2)

A participant receives the link, opens it, and sees the title, the message, and the candidate
days together with the answer form. They enter a display name, mark each day as *yes*, *no*, or
*maybe*, and submit — all in one session, without registering, without an email address, and
without leaving the page they landed on.

**Why this priority**: This is the product's reason to exist. A poll nobody can answer is a
demo; this turns it into the tool.

**Independent Test**: Open a poll link in a browser with no session and no stored credentials,
submit a complete response, and confirm it was recorded. Verifiable without US3.

**Acceptance Scenarios**:

1. **Given** a valid poll link, **When** a participant with no account and no session opens it,
   **Then** the answer form is visible immediately, with no login, consent gate, or interstitial
   between the link and the form.
2. **Given** the answer form, **When** the participant enters a name and marks every day,
   **Then** the response is stored and a confirmation is shown.
3. **Given** the answer form, **When** the participant submits without a name, **Then** the
   response is rejected and the missing input is named.
4. **Given** the answer form, **When** the participant leaves a day unanswered, **Then** that
   day is recorded as *no answer* and is distinguishable from *no*.
5. **Given** an unknown or malformed poll link, **When** it is opened, **Then** a neutral
   not-found page is shown that reveals nothing about whether such a poll ever existed.
6. **Given** a participant submits a response, **When** the confirmation appears, **Then** it
   includes a personal link with which that response — and only that response — can be changed
   later.

---

### User Story 3 - The creator sees who can attend when (Priority: P3)

The creator opens the poll in the admin area and sees a grid: participants down one axis,
candidate days across the other, each cell showing that person's answer. Per day, the number of
*yes*, *maybe* and *no* answers is totalled, so the best day is visible at a glance. The same
grid is visible to anyone holding the participant link (FR-036) — what the admin area adds is
the list of all polls and the ability to delete them, not privileged sight of the answers.

**Why this priority**: Collecting answers nobody reads is pointless, but the collecting has to
work first. Depends on US2 for data, and is separately verifiable once responses exist.

**Independent Test**: With two responses recorded, open the poll as the creator and confirm the
grid shows both and totals each day correctly.

**Acceptance Scenarios**:

1. **Given** a poll with several responses, **When** the creator opens it, **Then** every
   response appears with its name and its per-day answers.
2. **Given** a poll with responses, **When** the creator views it, **Then** each day shows the
   count of *yes*, *maybe* and *no*.
3. **Given** a poll with no responses yet, **When** the creator opens it, **Then** an empty
   state is shown rather than an error or a blank grid.
4. **Given** a visitor with neither the participant link nor a creator session, **When** they
   attempt to open the results through the admin area, **Then** access is refused and no
   participant name or answer is disclosed. Holding the participant link is what grants sight
   of the grid (FR-036) — authentication is what grants the admin area.

---

### User Story 4 - A participant corrects their answer (Priority: P4)

A participant realises they got a day wrong. Using the personal link from their confirmation,
they reopen their own response, change it, and save — still without an account.

**Why this priority**: Wrong data is worse than missing data, and Principle I forbids solving
this by identifying the participant. It is not needed for the first usable version.

**Independent Test**: Submit a response, follow the personal link, change one day, and confirm
the change is stored without a second response appearing.

**Acceptance Scenarios**:

1. **Given** a personal response link, **When** the participant opens it, **Then** their
   previous answers are shown, prefilled and editable.
2. **Given** a changed answer, **When** it is saved, **Then** the existing response is updated
   and no additional response is created.
3. **Given** someone without the personal link, **When** they attempt to change that response,
   **Then** the attempt is refused.
4. **Given** a personal link for a deleted poll, **When** it is opened, **Then** the same
   neutral not-found page is shown.

---

### User Story 5 - Polls do not accumulate forever (Priority: P5)

The creator can delete a poll and everything answered in it. Independently, a poll that is long
past its last candidate day is removed automatically, so answers do not linger indefinitely.

**Why this priority**: Constitution Principle IV requires every survey to have a defined
retention outcome, and requires deletion to actually remove responses rather than hide them.
It is the last piece needed, not the first.

**Independent Test**: Create a poll, add a response, delete the poll, and confirm the response
is gone and both the participant link and the personal response link stop working.

**Acceptance Scenarios**:

1. **Given** a poll with responses, **When** the creator deletes it, **Then** the poll and all
   its responses are removed, not merely hidden.
2. **Given** a deleted poll, **When** its participant link is opened, **Then** the neutral
   not-found page is shown.
3. **Given** a poll whose retention period has elapsed, **When** the retention process runs,
   **Then** the poll and its responses are removed without any manual action.
4. **Given** a creator is about to delete a poll, **When** they trigger deletion, **Then** they
   must confirm, and the number of responses that will be destroyed is stated.

---

### Edge Cases

- **Two participants use the same display name**: both responses are kept and distinguishable;
  a name is a label, never an identity.
- **The same person answers twice without their personal link**: a second, independent response
  is created. Principle I forbids preventing this by identifying the participant.
- **A poll has no candidate day left in the future**: it still accepts answers; the system does
  not decide that a poll is over.
- **A day boundary is crossed while someone is answering**: the candidate days on offer do not
  change mid-session. A day is a fixed label on the poll, not a value recomputed against the
  current clock.
- **A summer-time changeover falls between candidate days**: day boundaries and the retention
  deadline remain correct, because FR-011b resolves them against a zone rather than a fixed
  offset.
- **A poll is past its deadline but not yet erased**: it is already unreachable, because expiry
  is checked on access (FR-039b) and does not wait for the erasure process.
- **The erasure process does not run for several days**: nothing becomes reachable again; only
  the physical removal is late. Access has been refused since the deadline passed.
- **A participant submits while the creator is deleting the poll**: the answer is either stored
  and then deleted with the poll, or rejected with the neutral not-found page — never an error
  page exposing internals.
- **A very large number of candidate days**: refused above 100, with the limit named in the
  message (FR-015).
- **A very long title, message or participant name**: refused above 300, 2000 and 100 characters
  respectively, enforced on the server and not only in the form (FR-015).
- **Two participants submit simultaneously**: both responses are stored; neither overwrites the
  other.
- **A guessed participant link**: the token space is large enough that guessing is impractical,
  and a failed guess is indistinguishable from a deleted poll.
- **A day nobody answered**: all three counts are zero and every cell in that column is empty.
  This is a normal state, not an error, and must not be confused with a day everyone rejected.
- **A poll at its maximum size**: 1000 responses across 100 candidate days must still render a
  usable results view and a usable answer form; this is the sizing case the presentation has to
  survive, not an error case.
- **The 1001st participant**: submission is refused with an explicit "poll is full" message. The
  answer is never accepted and then dropped.
- **A leaked participant link is used to flood the poll**: the rate limit caps a single source
  at 10 answers per hour, and the creator can remove what got through one response at a time
  instead of destroying the whole poll.
- **A reader who cannot distinguish colours**: the grid must stay readable, which is why FR-053
  requires a character or word alongside any colour coding.
- **Several genuine participants answer from one network**: they share a request source and can
  collectively hit the rate limit. This is accepted: the limit is deliberately generous enough
  that ten answers within an hour from one household or office is unlikely, and the refusal
  message states when to retry rather than failing silently.
- **Session expires while the creator is filling in the form**: after 8 hours of inactivity the
  creator is asked to authenticate again and the entered values are not silently discarded.
- **The operator locks themselves out**: after 5 failed attempts they must wait 15 minutes.
  There is deliberately no reset path — with a single account there is nobody to authorise one,
  and a self-service reset would become the weakest way in.

## Requirements *(mandatory)*

### Functional Requirements

**Creator access**

- **FR-001**: The admin area MUST require authentication for every function it offers.
- **FR-002**: Unauthenticated requests to any admin function MUST be refused without revealing
  whether a given poll, participant, or account exists.
- **FR-003**: Credentials MUST be stored only in a form from which the original cannot be
  recovered, and MUST never appear in logs or in any response.
- **FR-004**: A failed sign-in MUST NOT disclose which part of the input was wrong.
- **FR-005**: After 5 consecutive failed sign-in attempts the account MUST be locked for 15
  minutes. During the lockout, a correct password MUST also be refused, so that the lockout
  cannot be used to test whether a password was right.
- **FR-005a**: The lockout MUST reset after a successful sign-in and MUST expire on its own
  after 15 minutes without any manual intervention — there is no unlock function and no reset
  path, since there is no second account to perform one.
- **FR-006**: A creator session MUST expire after 8 hours without activity, after which
  authentication is required again.
- **FR-007**: A creator MUST be able to end their session deliberately.

**Creating a poll**

- **FR-008**: A poll MUST have a title. Creation without one MUST be refused, naming the
  missing input.
- **FR-009**: A poll MAY have a short message. Its absence MUST NOT prevent creation.
- **FR-010**: A poll MUST have at least one candidate day. Creation without one MUST be refused.
- **FR-011**: Candidate days MUST be whole calendar days. This feature introduces no times of
  day; a participant answers for a day, not for an hour.
- **FR-011a**: Every day boundary in this feature MUST be resolved against the fixed zone
  Europe/Berlin — what counts as "today", what counts as past (FR-014), and when the retention
  deadline falls (FR-039). A candidate day therefore denotes the same day for every participant,
  wherever they open the link.
- **FR-011b**: The fixed zone MUST account for summer time, so that a day boundary is correct in
  both halves of the year without manual adjustment.
- **FR-012**: A day selected more than once MUST be stored exactly once.
- **FR-013**: Candidate days MUST be presented in chronological order regardless of the order in
  which they were selected.
- **FR-014**: Days in the past MUST be permitted, so that a poll can be created for a period
  already under way. "Past" is determined against the zone fixed in FR-011a.
- **FR-015**: The following limits MUST be enforced on the server, not only in the form, and
  exceeding one MUST produce a message naming the limit:

  | Bounded value | Limit |
  |---|---|
  | Title | 300 characters |
  | Short message | 2000 characters |
  | Display name | 100 characters |
  | Candidate days per poll | 100 |
  | Responses per poll | 1000 |

- **FR-015a**: Once a poll has reached 1000 responses, further submissions MUST be refused with
  a message that says the poll is full, and MUST NOT silently discard the answer.
- **FR-016**: On creation the system MUST generate a participant link containing an unguessable
  token, and MUST display it to the creator in a form that can be copied.
- **FR-017**: The token MUST be generated from a space large enough that guessing a valid link
  is impractical, and MUST NOT be derived from the title, the days, or a sequential counter.
- **FR-018**: The admin area MUST list all polls, each with its participant link and its
  retention deadline. With a single operator account there is no ownership filter.

**Answering — Principle I is binding here**

- **FR-019**: Opening a valid participant link MUST present the title, the message, the
  candidate days, and the answer form together, in one page load.
- **FR-020**: Answering MUST NOT require an account, a sign-in, an email address, a
  confirmation step, or any installation.
- **FR-021**: No step of any kind MUST be placed between the link and the answer form.
- **FR-022**: A participant MUST provide a display name. It is a label only and MUST NOT be
  treated as an identity or matched against other responses.
- **FR-023**: For each candidate day a participant MUST be able to record exactly one of: has
  time, has no time, might have time.
- **FR-024**: A day left unanswered MUST be recorded as *no answer* and MUST remain
  distinguishable from *has no time*, both in storage and on screen.
- **FR-024a**: In the grid, *no answer* MUST be shown as an empty cell that is visually distinct
  from every answered state. It MUST NOT be rendered in a way that could be mistaken for *has
  no time*.
- **FR-025**: A complete response MUST be submittable in a single session without navigating
  away from the poll page.
- **FR-026**: On submission the system MUST issue a personal response link containing its own
  unguessable token, and MUST show it to the participant.
- **FR-027**: An unknown, malformed, expired, or deleted participant link MUST produce one
  neutral not-found response, identical in all four cases.
- **FR-027a**: Submissions MUST be rate-limited to at most 10 per hour per request source, so
  that a leaked link cannot be used to fill a poll to its 1000-response limit.
- **FR-027b**: The request source MAY be used for FR-027a only transiently, in memory, for the
  duration of the limiting window. It MUST NOT be written to durable storage and MUST NOT be
  associated with any response — FR-042 is not relaxed by this requirement.
- **FR-027c**: A submission refused by the rate limit MUST say that too many answers were sent
  and when the participant may try again, and MUST NOT accept and then discard the answer.

**Changing an answer**

- **FR-028**: A personal response link MUST allow its holder to view and change that response.
- **FR-029**: The personal response link MUST NOT grant access to any other response, nor to
  any admin function.
- **FR-030**: Changing a response MUST update the existing one and MUST NOT create a second.
- **FR-031**: Without the personal response link, no participant-facing route MUST permit
  changing or deleting an existing response.

**Results**

- **FR-032**: The creator MUST see every response with its display name and its answer for each
  candidate day.
- **FR-033**: For each candidate day the system MUST show how many participants answered *has
  time*, *might have time*, and *has no time*. *No answer* is deliberately not counted, so these
  three numbers need not add up to the number of responses.
- **FR-033a**: Because the totals alone do not reveal how many people responded, the grid MUST
  show every response as its own row (FR-032, FR-036), so that the number of responses is
  readable directly from the grid. No separate fourth count is required.
- **FR-034**: A poll without responses MUST show an explicit empty state.
- **FR-035**: A newly submitted response MUST appear in the results without any manual
  refresh action beyond reopening or reloading the results view.
- **FR-036**: Anyone holding the participant link MUST see the full grid: every response with
  its display name and its per-day answers, together with the per-day totals from FR-033.
- **FR-036a**: Before a participant enters a display name, the page MUST state plainly that the
  name and the answers will be visible to everyone who has the link. A participant must not
  discover this only after submitting.
- **FR-036b**: The grid MUST be readable on the participant page without submitting a response,
  so that someone can see the state of the poll before deciding to answer.
- **FR-036c**: The results grid MUST remain readable and responsive at the limits of FR-015 —
  1000 responses across 100 candidate days, that is 100,000 cells. Rendering the entire grid at
  once is not required; any presentation that lets a reader reach any participant and any day
  without the page becoming unusable satisfies this.

**Retention and deletion**

- **FR-037**: The creator MUST be able to delete a poll, and deletion MUST remove the poll and
  all of its responses rather than hiding them.
- **FR-037a**: The creator MUST be able to delete a single response without affecting the poll
  or any other response. Deletion MUST remove it rather than hiding it, and the corresponding
  personal response link MUST afterwards produce the neutral not-found response.
- **FR-037b**: Deleting a response MUST update the per-day totals accordingly.
- **FR-038**: Deletion MUST require an explicit confirmation that states how many responses will
  be destroyed.
- **FR-039**: Every poll MUST have a defined retention outcome. A poll and all of its responses
  MUST be removed automatically once 30 days have elapsed since the end of its last candidate
  day, where the day ends at 23:59:59 in the zone fixed by FR-011a.
- **FR-039a**: The retention deadline MUST be derived from the last candidate day at creation
  and MUST be visible to the creator, so that the disappearance of a poll is never a surprise.
- **FR-039b**: Expiry MUST take effect on access. Every request for a poll, for its results, and
  for any personal response link MUST be checked against the retention deadline, so that a poll
  becomes unreachable at the moment the deadline passes rather than when a job happens to run.
- **FR-039c**: A background process MUST erase expired polls and their responses at least once
  per day, so that expired data is genuinely removed and not merely made unreachable. Principle
  IV requires deletion to remove the responses, not to hide them.
- **FR-039d**: The erasure process MUST be safe to run repeatedly and MUST record how many polls
  it removed (FR-043a).
- **FR-040**: After deletion or expiry, both the participant link and every personal response
  link MUST produce the neutral not-found response.

**Presentation and operability**

- **FR-050**: Every interactive control on both the admin and the participant pages MUST be
  reachable and operable using the keyboard alone.
- **FR-051**: Every interactive control MUST carry a text label that names what it does; an icon
  or a colour alone is not a label.
- **FR-052**: The control that currently has keyboard focus MUST be visibly marked.
- **FR-053**: The three answer states MUST NOT be distinguished by colour alone. Each MUST also
  carry a distinguishing character or word, so that the grid remains readable without colour
  perception. The same applies to the empty *no answer* cell of FR-024a: its distinction from
  *has no time* MUST survive without colour.
- **FR-054**: This feature commits to FR-050 to FR-053 only. It makes no claim to any wider
  accessibility standard; contrast ratios, focus order, and screen-reader semantics beyond the
  labels of FR-051 are out of scope.

**Data and privacy**

- **FR-041**: The system MUST store only what the poll asks for: the display name and the
  per-day answers.
- **FR-042**: Participant IP addresses and user-agent strings MUST NOT be persisted alongside
  responses.
- **FR-043**: Logs MUST NOT contain response content, display names, or link tokens.
- **FR-043a**: The system MUST log each of the following, identified only by a technical
  identifier: a successful sign-in, a failed sign-in, a lockout becoming active, a poll being
  created, a poll being deleted, a single response being deleted, a submission refused by the
  rate limit, and each automatic retention deletion with the number of polls removed.
- **FR-043b**: No entry required by FR-043a may contain a display name, an answer, a link token,
  or a request source. FR-043 constrains FR-043a; where they appear to conflict, FR-043 wins.
- **FR-044**: No participant-facing or admin-facing page may load assets from, or send data to,
  any third party.

**Creator accounts**

- **FR-045**: The system MUST have exactly one creator account, configured at deployment
  through environment configuration. There is no registration, no user management, and no
  per-creator ownership: every poll belongs to the single operator.
- **FR-045a**: The configured credential MUST be supplied in a form from which the original
  password cannot be recovered, so that deployment configuration never contains a plaintext
  password.
- **FR-045b**: Changing the credential MUST take effect through configuration; the system is
  not required to offer a password-change function inside the admin area.

**Verification**

- **FR-046**: Every behaviour in this specification MUST be introduced test-first.
- **FR-047**: An automated end-to-end test MUST prove that a complete response can be submitted
  from a bare link in a browser with no account, no session, and no stored credentials.
- **FR-048**: An automated test MUST assert that every admin function refuses unauthenticated
  access and discloses nothing.
- **FR-049**: An automated test MUST assert that deletion removes responses, by confirming they
  are no longer retrievable by any route.

### Key Entities

- **Poll (Terminfindung)**: A dated question put to a group. Attributes: title, optional short
  message, creation moment, retention deadline, participant token. Owns its candidate days and
  its responses; deleting it destroys both.
- **Candidate Day**: One whole calendar day offered for selection within a poll. Unique within
  its poll; ordered chronologically for display.
- **Response**: One participant's answers to one poll. Attributes: display name, submission
  moment, personal edit token. Holds exactly one answer per candidate day. Carries no identity,
  no contact detail, and no network metadata.
- **Answer**: The intersection of one response and one candidate day. Exactly one of: has time,
  might have time, has no time, no answer.
- **Creator Account**: The single operator identity permitted to create polls, delete polls and
  responses, and read the admin area. Configured at deployment (FR-045); not a stored domain
  entity with a lifecycle, and not owned per poll.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A participant who receives a link can submit a complete response for a five-day
  poll in under 60 seconds, measured from opening the link.
- **SC-002**: The number of steps between opening the participant link and reaching the answer
  form is zero.
- **SC-003**: The number of accounts, sign-ins, email addresses, or installations required of a
  participant is zero.
- **SC-004**: A creator can create a poll with a title, a message and five days in under two
  minutes.
- **SC-005**: 100% of admin functions refuse unauthenticated access, and none of those refusals
  discloses whether a poll, participant, or account exists.
- **SC-006**: Participant and response tokens are drawn from a space of at least 2^120
  possibilities, making a successful guess impractical.
- **SC-007**: Deleting a poll removes 100% of its responses; none remains retrievable by any
  route afterwards.
- **SC-008**: A participant can change a previously submitted answer without an account, and
  doing so leaves the total number of responses unchanged.
- **SC-009**: The results view reflects a newly submitted response on the next reload in 100% of
  cases.
- **SC-010**: Zero stored response records contain an IP address or user-agent string.
- **SC-011**: Zero log entries contain a display name, an answer, or a link token.
- **SC-012**: Unknown, malformed, expired, and deleted links produce byte-identical not-found
  responses, so none of the four can be told apart.
- **SC-013**: A participant sees, before entering a name, that the name and answers will be
  visible to everyone holding the link — verified by an automated check of the answer page.
- **SC-014**: A poll whose last candidate day was more than 30 days ago is no longer retrievable
  by any route, verified without manual intervention.
- **SC-015**: The deployment configuration contains no plaintext password, verified by a check
  that the configured credential is not usable as one.
- **SC-016**: With a poll at the FR-015 limits — 1000 responses across 100 candidate days — the
  results view becomes usable within 5 seconds and permits reaching any participant and any day.
- **SC-017**: Each of the five limits in FR-015 is enforced server-side, verified by a request
  that bypasses the form and is rejected with the limit named.
- **SC-018**: A session with no activity for 8 hours no longer grants access to any admin
  function.
- **SC-019**: The 6th consecutive failed sign-in is refused, and a correct password is also
  refused for the following 15 minutes.
- **SC-020**: The 11th submission from one source within an hour is refused, and the refusal
  states when the participant may retry.
- **SC-021**: Zero stored response records can be associated with a request source, verified by
  inspecting everything persisted for a response.
- **SC-022**: Deleting a single response leaves every other response intact, updates the per-day
  totals, and makes that response unreachable by its personal link.
- **SC-023**: Each of the eight events in FR-043a produces exactly one log entry when it occurs.
- **SC-024**: Across a run that exercises every event in FR-043a, zero log entries contain a
  display name, an answer, a link token, or a request source.
- **SC-025**: A complete response can be submitted using the keyboard alone, without a pointing
  device.
- **SC-026**: Rendered in greyscale, the three answer states remain distinguishable from one
  another in the grid and in the answer form.
- **SC-027**: A candidate day shows the same date to every viewer regardless of the time zone
  their device reports.
- **SC-028**: Day-boundary behaviour is identical either side of a summer-time changeover,
  verified by tests fixed at both a winter and a summer date.
- **SC-029**: An unanswered cell is distinguishable from a *has no time* cell both in colour and
  in greyscale.
- **SC-030**: For a day where every participant answered *has no time*, and a day nobody
  answered at all, the two columns are told apart by a reader without consulting the raw data.
- **SC-031**: A poll one second past its retention deadline is already unreachable through the
  participant link, the results view, and every personal response link — without waiting for the
  erasure process.
- **SC-032**: Within 24 hours of a poll's deadline passing, none of its data remains stored,
  verified by inspecting storage directly rather than by attempting access.

## Assumptions

- **Whole days only.** The description says "Tage"; times of day and time ranges are out of
  scope, and Doodle's time-slot polls are a possible later feature. Time zones are *not* out of
  scope, despite the original wording: comparing a day to "now" requires one, so FR-011a fixes
  a single zone rather than leaving it undefined.
- **The display name is free text**, as in Doodle. Nothing verifies it, and two participants may
  use the same one.
- **Repeat answering is not prevented.** Without identifying participants there is no honest way
  to stop it, and Principle I forbids identifying them. The personal response link is the
  supported way to revise an answer.
- **Answers are per day, not per person per day per time** — the poll is one question repeated
  across days.
- **A day may be left unanswered and this is not counted.** The per-day totals describe only the
  people who took a position. How many responded at all is read from the number of rows in the
  grid, not from the totals.
- **No notifications.** Nobody is emailed when a response arrives; the creator looks.
- **No closing or freezing of a poll.** A poll accepts answers until it is deleted or expires.
- **The technology stack is fixed** by constitution v1.1.1 and instantiated by feature 001. This
  specification does not revisit it.
- **Deployment remains out of scope**, as in feature 001. The admin area being protected does not
  imply it is hosted anywhere.

## Resolved Decisions

- **One operator account (FR-045)**. Configured through environment variables at deployment, no
  registration and no user management. It matches how the product is meant to run — you operate
  it for your own group — and keeps this feature from growing a registration flow with its own
  chain of questions (who may register, invitation, approval). Consequence: changing the
  password is a configuration change, not an in-app action.
- **Participants see the full grid (FR-036)**. Chosen over aggregate-only and nothing-at-all
  because it is what makes a Doodle-style poll work: people converge by seeing where the group
  already is. Consequence, and the reason FR-036a exists: a display name is public to everyone
  holding the link, so the page must say so *before* the name is entered rather than after.
  A second consequence: results are no longer creator-only, which is why User Story 3's fourth
  acceptance scenario distinguishes holding the link from holding a session.
- **30 days after the last candidate day (FR-039)**. Long enough to survive the event and any
  follow-up, short enough that answers do not accumulate for years. Satisfies Principle IV's
  requirement of a defined retention outcome with an actual expiry rather than only a deletion
  path.

## Dependencies

- Feature `001-platform-scaffold`: the running two-container system, the `/api/v1` convention,
  the German i18n layer, and the automated test suites this feature extends.
- The project constitution at `.specify/memory/constitution.md` (v1.1.1). Principle I governs
  every participant-facing requirement here; Principle IV governs retention and logging.

## Out of Scope

- Time-of-day slots, time ranges, and time zones.
- Notifying anyone by email or any other channel.
- Closing, freezing, or archiving a poll without deleting it.
- Exporting results in any format.
- Editing a poll's title, message, or candidate days after creation.
- Preventing the same person from answering more than once.
- Any survey type other than date polls — free-text and single-value questions from the product
  description are a separate feature.
- Deployment, hosting, and release automation.
