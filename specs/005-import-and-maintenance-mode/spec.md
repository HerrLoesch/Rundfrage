# Feature Specification: Importing Exported Data, and a Maintenance Mode

**Feature Branch**: `005-import-and-maintenance-mode`
**Created**: 2026-09-04
**Status**: Draft
**Input**: User description: "Ich will auf der Adminseite eine Import Möglichkeit für die exportierten Daten. Außerdem soll die Seite in einen Wartungsmodus geschaltet werden können, wodurch die Nutzer nur eine Seite sehen, die darauf hinweist, dass gerade eine Wartung stattfindet, statt der entsprechenden Umfrage zu zeigen."

## Summary

Two capabilities for the operator, which belong together because the second is what makes the
riskiest part of the first safe to perform.

**Import** reverses a one-way door. Today what the system produces can be downloaded and never put
back: `specs/003-sqlite-and-export` states plainly that *"there is no import"*, and the export
format's version number is called *"a signal, not a promise"* precisely because nothing was ever
committed to reading it back. This feature makes that commitment.

**Maintenance mode** replaces the participant's view with a notice that work is in progress. The
operator needs it for exactly the moment the first capability creates: while data is being
replaced, a participant answering into a poll that is about to be overwritten is answering into
nothing.

### Two imports, two different purposes

The system produces two downloadable artefacts, and this feature accepts both — but they are not
two flavours of one thing. They differ in what they are *for*, and everything else follows:

| | Per-poll JSON export | Whole-storage backup |
|---|---|---|
| **Purpose** | Onward processing — reading, analysing, moving one poll | **Restoration** — putting the system back as it was |
| Contains | One poll, readable, **no tokens** | Everything, opaque, **tokens included** |
| Importing it | Creates a **new** poll with a **new** link | Replaces **all** data; every link works again |
| Scope of effect | Additive — nothing existing is touched | Total and irreversible without a prior backup |
| Needs maintenance mode | No | **Required** — refused without it |

The JSON export stays tokenless. Its job is onward processing, and link continuity is not part of
that job — so the deliberate omission in 003 FR-015 stands unchanged, and an imported JSON poll
always gets a fresh participant link. Restoration is the backup's job, and because the backup
carries the tokens already, links survive it without any change to any format.

### Nothing is dropped in silence

An import reports what it did. Where something in the file was not taken, the summary names it and
says why — a poll already past its retention date, a response that answered a day the poll does not
have, a value the system does not recognise. Skipping is allowed; skipping quietly is not. This is
what makes a tolerant import honest instead of lossy.

## Clarifications

### Session 2026-09-04

- Q: Which of the two exports does "die exportierten Daten" mean? → A: Both, as two separate
  functions — the per-poll JSON export and the whole-storage backup.
- Q: Should the participant link survive an import? → A: Split by purpose. A **backup** must
  restore every link, because it exists to put the system back as it was. A **JSON** import need
  not, because that format exists for onward processing. The JSON export therefore stays tokenless
  and imported JSON polls always receive new links.
- Q: What happens when an imported poll's candidate days have all already passed? → A: It is
  reported rather than decided in advance. After an import the operator is shown a summary of what
  was not taken and why; a poll already past its retention date is one such reason.
- Q: How long may an uploaded import file remain on the system? → A: Only for the operation.
  It is removed as soon as the import finishes, on the failing path as well as the succeeding one
  — the mirror of 003 FR-021, which already says nothing is kept after a backup is downloaded.
- Q: May a restore run while maintenance mode is off? → A: No — it is refused, not merely warned
  about. Requiring maintenance mode makes the dependency between the two capabilities a rule rather
  than a habit, and removes the case of a participant answering into data being replaced underneath
  them instead of having to handle it.
- Q: What does a restore do with polls in the backup that are already past their retention date?
  → A: It takes them as they are and names them in the summary. A restore reproduces the backup;
  filtering during it would make the restored state deliberately unequal to the backup, which
  FR-016 and SC-006 forbid. The ordinary retention sweep removes them afterwards, by the same rule
  that governs every other poll. This differs from FR-012 on purpose: a JSON import creates
  something new, while a restore reproduces something that existed.
- Q: Does an import run as one request the operator waits on, or as a job? → A: One request. The
  summary is that request's answer and is not kept afterwards. The volumes involved are small and
  Principle III asks for the simplest thing that meets the requirement; a job store with state,
  progress and history would be a second body of persistent data for an operation that takes a
  moment. FR-020 already secures correctness independently of whether the connection holds.
- Q: Is there a maximum accepted size for an uploaded file? → A: No, on neither path. A JSON file
  is bounded after parsing by the content limits of FR-014; a backup is bounded by nothing, and
  FR-019's structural check necessarily runs once the file has been received in full. Accepted
  consequence: an oversized upload can fill the volume the storage lives on. What it cannot do is
  cost data — FR-020 and FR-021 keep the existing data in place until a restore has succeeded, so
  the exposure is availability, not loss. Recorded here so the trade is visible rather than
  rediscovered.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Put an exported poll back into the system (Priority: P1)

The operator has a JSON file this system produced earlier. They open the admin area, choose the
file, and confirm. The poll appears in the list with its title, its days and the responses the file
held, and a **new** participant link to share. A summary states what was taken and names anything
that was not, with the reason.

**Why this priority**: It is the capability that does not exist at all today, it is additive — it
cannot damage anything that already exists — and it is the one the request names first.

**Independent Test**: Export a poll, delete it, import the file, and compare the resulting poll's
title, days, participant names and per-day answers against the original, field by field. Fully
testable without maintenance mode or backup restore existing.

**Acceptance Scenarios**:

1. **Given** a file this system exported, **When** the operator imports it, **Then** a new poll
   exists whose title, message, candidate days and responses match the file exactly.
2. **Given** an imported poll, **When** the operator opens the poll list, **Then** it is shown with
   a participant link that differs from the original poll's link.
3. **Given** an imported poll, **When** a participant opens its link, **Then** they can answer, and
   the answers from the file are visible alongside theirs.
4. **Given** a file whose candidate days have all passed, **When** the operator imports it, **Then**
   the summary reports that it was not taken and that its retention date has already elapsed.
5. **Given** a file containing a response that answers a date the poll does not list, **When** the
   operator imports it, **Then** the poll is created with everything else, that one answer is
   absent, and the summary names it (FR-012 — the skipped answer was never accepted, so FR-013's
   "together" is not broken by its absence).
6. **Given** the same file imported twice, **When** the operator opens the poll list, **Then** two
   separate polls are listed and neither has damaged the other.
7. **Given** a file that is not a valid export at all, **When** the operator imports it, **Then**
   nothing is created and the refusal carries the code identifying which defect it was (FR-006).

---

### User Story 2 - Take the participant side down for maintenance (Priority: P2)

The operator switches maintenance mode on in the admin area. From that moment anyone opening a poll
link sees a notice that maintenance is in progress instead of the poll. The operator keeps working
in the admin area throughout, and switches it back off when done.

**Why this priority**: Independently valuable — worth having on its own, for every occasion the
operator needs the data to hold still. It is second because polls are not being answered
continuously, so the window it protects is intermittent. It is a prerequisite for User Story 3.

**Independent Test**: Switch it on, open a participant link in a browser with no session, confirm
the notice replaces the poll, switch it off, confirm the poll is back and unchanged.

**Acceptance Scenarios**:

1. **Given** maintenance mode is on, **When** a participant opens a poll link, **Then** they see the
   maintenance notice and no part of the poll — not the title, not the days, not the results.
2. **Given** maintenance mode is on, **When** a participant opens their personal link, **Then** they
   see the same notice and their existing answer is left untouched.
3. **Given** maintenance mode is on, **When** the operator opens the admin area, **Then** it works
   completely, including signing in and switching maintenance mode off.
4. **Given** maintenance mode is on, **When** a participant submits an answer, **Then** it is
   refused and nothing is recorded — the participant is never told an answer was saved when it was
   not.
5. **Given** maintenance mode was switched on, **When** the application is restarted, **Then**
   maintenance mode is still on.
6. **Given** maintenance mode is on, **When** the container runtime checks whether the application
   is healthy, **Then** it is reported healthy.

---

### User Story 3 - Restore the whole system from a backup (Priority: P3)

Something went wrong — data was lost, a volume was mounted empty, an import went badly. The
operator switches on maintenance mode, uploads a backup file, is shown what it contains and what
will be lost, and confirms. Afterwards every poll and every link is as it was when the backup was
taken. A summary reports what came back and anything the restore could not take.

**Why this priority**: The highest value when it is needed and the highest risk at every other
moment — it replaces everything, and a mistaken restore is itself a data loss. It goes last because
it depends on maintenance mode existing, and because the manual route documented in the README
still works in the meantime.

**Independent Test**: Take a backup, create a further poll, restore the backup, and confirm the
system matches the backup exactly — including that a participant link issued before the backup
still reaches its poll, and that the poll created afterwards is gone.

**Acceptance Scenarios**:

1. **Given** a backup taken earlier, **When** the operator restores it, **Then** every poll,
   response and answer in that backup is present and the system holds nothing that was not in it.
2. **Given** a participant link that worked when the backup was taken, **When** the backup is
   restored, **Then** that same link reaches its poll again — the link is not reissued.
3. **Given** a personal link handed out before the backup was taken, **When** the backup is
   restored, **Then** the participant can still change their own answer through it.
4. **Given** polls created after the backup was taken, **When** the operator is asked to confirm,
   **Then** they are told those polls will be lost, and how many, before anything is replaced.
5. **Given** maintenance mode is off, **When** the operator tries to restore a backup, **Then** it
   is refused, the reason names maintenance mode, and no data is replaced.
6. **Given** a file that is not a backup this system produced, **When** the operator tries to
   restore it, **Then** it is refused and the existing data is untouched.
7. **Given** a restore that fails partway for any reason, **When** the operator looks at the system,
   **Then** it holds either the previous data or the restored data — never a mixture of both.
8. **Given** maintenance mode is on and a restore completes, **When** the operator switches
   maintenance mode off, **Then** it was the operator who switched it off — the restore did not
   change it.
9. **Given** a backup containing a poll already past its retention date, **When** it is restored,
   **Then** the poll is present, the summary names it as already expired, and the next retention
   sweep removes it.

---

### Edge Cases

**JSON import**

- A file that is not valid JSON, or is a different document entirely.
- A file whose format version is higher than this system understands — refused rather than read
  optimistically, because a raised version means a field changed meaning (003 FR-020b).
- A response answering a date that is not among the poll's candidate days.
- An availability value that is not one of the three the system knows.
- A file exceeding a documented limit: more than 100 candidate days, more than 1000 responses, a
  title over 300 characters, a message over 2000, a display name over 100.
- A file with zero responses — valid, and imports as an empty poll.
- Duplicate dates among the candidate days; a response answering the same day twice with
  conflicting values.
- A file where *everything* is skippable, leaving a poll with no responses at all — the summary
  must make that outcome unmistakable rather than reporting a successful import.
- Storage becomes unreachable partway through: nothing partial may survive.

**Backup restore**

- A file that is a valid archive of some other system, or a truncated download.
- A backup taken from an older version of the system, whose stored shape has since changed.
- A backup larger than the space available. No size is refused up front, so this surfaces as a
  storage failure during the upload; FR-021 is what keeps the existing data intact through it.
- The operator restores a backup without switching maintenance mode on first — refused under
  FR-024, and worth testing as the negative case it now is.
- Maintenance mode is switched off from a second browser tab while a restore is running.
- The system is restarted mid-restore.
- A backup that contains polls already past their retention date — restored as they are under
  FR-016a, reported, then swept. Worth testing that the sweep does run afterwards.
- Restoring the same backup twice in a row.

**Maintenance mode**

- A participant is on the answer form when maintenance is switched on, and submits afterwards.
- Maintenance mode is switched on twice, or off when already off.
- The operator's session expires while maintenance mode is on — they must still be able to sign in
  to switch it off.
- Storage is unreachable *and* maintenance mode is on: the notice must still be shown, and the two
  states must not be reported as each other.
- A poll passes its retention deadline while maintenance mode is on.

## Requirements *(mandatory)*

### Functional Requirements — Common to both imports

- **FR-001**: The admin area MUST offer importing, and MUST present the two kinds as distinct
  operations with distinct consequences. An operator MUST NOT be able to trigger a whole-system
  restore while believing they are adding a single poll.
- **FR-002**: Both imports MUST require an operator session and MUST NOT be reachable without one.
- **FR-003**: Every import MUST produce a summary once it finishes, stating what was taken and
  naming each thing that was not, with its reason.
- **FR-003a**: The summary MUST be delivered as the answer to the request that performed the
  import. It is not retained, and there is no record of past imports to return to. An operator who
  needs to keep it keeps it themselves.
- **FR-004**: An import that takes nothing at all MUST say so plainly. It MUST NOT be reported as a
  success on the grounds that it did not fail.
- **FR-005**: The content of an imported file MUST NOT appear in the logs. That an import happened,
  which kind, and whether it succeeded MAY be logged.
- **FR-005a**: An uploaded file MUST NOT outlive the import that reads it. It MUST be removed once
  the import finishes, whether it succeeded, was refused or failed partway. This matters most for a
  backup: that file carries every participant link and every personal link in the system, so a copy
  left behind is a second, unguarded original. Nothing is kept, in either direction (003 FR-021).
- **FR-006**: A refused file MUST be named as refused, and the refusal MUST carry a distinct code
  identifying the defect — not one generic failure for every cause. The codes are enumerated in
  `contracts/openapi.yaml`; each must be reachable by a test and rendered into German. "Named
  specifically enough to act on" is that enumeration, not a matter of judgement.

### Functional Requirements — Importing a per-poll JSON export

- **FR-007**: The system MUST accept files in the export format version it currently produces, and
  MUST refuse a file declaring a higher version rather than interpreting it.
- **FR-008**: A JSON import MUST create a **new** poll. It MUST NOT modify, merge into, or replace
  any poll that already exists.
- **FR-009**: The imported poll MUST reproduce the title, the message, every candidate day and
  every response with its display name and per-day answers, exactly as the file records them,
  except where FR-012 excludes an item.
- **FR-010**: A day a response did not answer MUST remain unanswered after import. No placeholder
  value may be invented for it.
- **FR-011**: The imported poll MUST receive a new participant link, and each imported response a
  new personal link. The operator MUST be told that links previously shared for the original poll
  do not reach the imported one.
- **FR-012**: Where part of a file cannot be taken — a response answering an unknown day, an
  unrecognised availability value, a poll already past its retention date — that part MUST be
  skipped and reported under FR-003, and the rest MUST still be imported.
- **FR-013**: A poll and everything accepted with it MUST be created together or not at all. This
  is not in tension with FR-012: what is skipped was never accepted, so "together" covers the poll,
  its days, and every response and answer that passed. If the poll itself cannot be created, no
  response or answer from that file may remain behind.
- **FR-014**: A JSON import MUST enforce every limit that creating a poll by hand enforces — the
  maximum number of candidate days and responses, and the maximum lengths of title, message and
  display name.
- **FR-015**: The imported poll MUST be given a retention deadline by the same rule as any other
  poll, and it MUST be visible in the poll list as for any other poll.

### Functional Requirements — Restoring a whole-storage backup

- **FR-016**: The system MUST accept a backup file it produced itself and restore the state that
  backup holds, replacing all current polls, responses and answers.
- **FR-016a**: A restore MUST take the backup's contents as they are, including polls already past
  their retention date. It MUST NOT filter them out — a restore that quietly kept less than the
  backup would not be a restore. The summary MUST name such polls, and the ordinary retention sweep
  MUST then remove them by the same rule that governs every other poll.
- **FR-017**: A restore MUST preserve every participant link and every personal link the backup
  contains. A link that worked when the backup was taken MUST work again afterwards.
- **FR-018**: Before anything is replaced, the operator MUST be shown what the backup contains and
  what will be lost — in particular how many polls and responses exist now that are not in the
  backup — and MUST confirm.
- **FR-019**: The system MUST verify that the file is a backup it can restore before replacing
  anything. A file that is not MUST leave the existing data untouched. The check runs once the file
  has been received in full — no size is refused up front (see Assumptions).
- **FR-020**: A restore MUST be all-or-nothing. At every moment the system MUST hold either the
  previous data or the restored data, never a mixture.
- **FR-021**: The system MUST preserve the data being replaced until the restore has succeeded, so
  that a failed restore does not cost the operator what they had.
- **FR-022**: A backup whose stored shape predates the current version MUST be brought up to the
  current shape as part of the restore, or refused with that stated as the reason. It MUST NOT be
  restored into a state the system cannot read.
- **FR-023**: A restore MUST NOT change whether maintenance mode is on. Whatever the operator set
  before the restore MUST still be in effect after it.
- **FR-024**: The system MUST refuse a restore while maintenance mode is off, naming the missing
  step. Requiring maintenance mode rather than advising it is what makes a participant answering
  into data that is being replaced impossible by construction, instead of a case to handle.

### Functional Requirements — Maintenance mode

- **FR-025**: The admin area MUST offer a control that switches maintenance mode on and off, and
  MUST show which state is in effect.
- **FR-026**: While maintenance mode is on, every participant-facing view MUST be replaced by a
  maintenance notice. No poll title, message, candidate day, response or result may be disclosed
  through any participant route.
- **FR-027**: While maintenance mode is on, the system MUST NOT accept a new response or a change
  to an existing one, and MUST NOT confirm one it did not record.
- **FR-028**: While maintenance mode is on, the admin area MUST remain fully usable, including
  signing in and switching maintenance mode off.
- **FR-029**: Maintenance mode MUST survive a restart of the application. A redeploy must not
  silently reopen the participant side.
- **FR-030**: Maintenance state MUST be held outside the data a restore replaces, so that restoring
  a backup cannot switch the participant side back on underneath the operator.
- **FR-031**: Maintenance mode MUST NOT cause the application to report itself unhealthy to the
  container runtime. Maintenance is a deliberate state, not a fault, and reporting it as one would
  have the deployment replaced or rolled back mid-maintenance.
- **FR-032**: The maintenance notice MUST say that maintenance is in progress and that answering
  will be possible again afterwards. It MUST NOT disclose which poll was requested, or whether a
  poll behind that link exists at all.
- **FR-033**: Switching maintenance mode on or off MUST take effect for the next request, without
  restarting the application.
- **FR-034**: Maintenance mode MUST NOT by itself delete, alter or expire any poll or response.
  Switching it off MUST leave the data exactly as it was.

### Key Entities

- **JSON export file**: One poll as a self-contained document — a format version, the moment it was
  taken, the poll's title and message, its candidate days in order, and each response with a display
  name and answers addressed by date. Carries no token of any kind and no internal identifier.
- **Backup file**: A complete, self-contained copy of everything the system holds at one instant,
  including the tokens that make links work. Opaque; meaningful only to this system. While
  uploaded it is as sensitive as the live system and exists only for the length of the restore.
- **Import summary**: What one import took, and each thing it did not, with the reason. It exists
  as the answer to the request that produced it and is not stored; there is no import history.
- **Maintenance state**: A single system-wide on/off condition, set by the operator, that outlives a
  restart and is untouched by a restore. It is not a property of any poll and not part of poll data.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator can import a previously exported poll in under 1 minute from opening the
  admin area, without consulting documentation.
- **SC-001a**: An import of a file at the documented maximum — 100 candidate days and 1000
  responses — finishes while the operator waits, without the request being abandoned.
- **SC-002**: A poll exported and then imported reproduces 100% of its title, message, candidate
  days, participant names and per-day answers — verified field by field, not by row count.
- **SC-003**: Every skipped item appears in the import summary with a reason. Across the full set of
  skip cases exercised in testing, 0 items are dropped without being reported.
- **SC-004**: No failed import leaves a partial poll behind — 0 partial polls across the full set of
  failure cases exercised in testing.
- **SC-005**: An operator can distinguish, before confirming, whether they are about to add one poll
  or replace everything — verified by the confirmation text naming the scope of the change.
- **SC-006**: After restoring a backup, 100% of participant links and personal links that worked
  when the backup was taken work again.
- **SC-007**: A failed or refused restore leaves the system holding exactly the data it held before
  — verified field by field, in 100% of failure cases exercised.
- **SC-007a**: No uploaded file remains anywhere on the system once an import has finished — 0
  leftovers across the success, refusal and mid-failure paths exercised in testing.
- **SC-008**: An operator can switch maintenance mode on and confirm a participant link shows the
  notice within 30 seconds.
- **SC-009**: While maintenance mode is on, 0 participant requests disclose any poll content, and 0
  answers are recorded.
- **SC-010**: Maintenance mode remains in effect across a restart, and across a backup restore, in
  100% of attempts.
- **SC-011**: The application is reported healthy by the container runtime for the entire duration
  of a maintenance period, including while a restore is running.

## Assumptions

- **One poll per JSON file.** The export produces a single poll per document, so a JSON import
  handles one poll per file. Importing several means importing several times.
- **Skipping applies at two levels**: a whole poll may be skipped (for instance, already past its
  retention date), and an individual response or answer within an otherwise good poll may be
  skipped. Both are reported the same way. A structural defect in the file — unreadable, or a
  format version too new — is not a skip but a refusal, and takes nothing.
- **Files come from this system.** A hand-written or foreign JSON file that happens to be valid
  will import; nothing is done to detect where a file came from. A backup file, being opaque, is
  verified as one this system can restore.
- **Importing the same JSON file twice is allowed** and produces two independent polls. There is no
  identifier in the export to deduplicate on, and inventing one would mean writing an identity into
  a file that deliberately carries none.
- **Response submission order is not preserved by a JSON import.** The export records no timestamps,
  so imported responses are ordered as the file lists them. A backup restore preserves them.
- **The operator account is unaffected by a restore.** Sign-in credentials are configuration, not
  poll data, so restoring a backup taken under different credentials does not change who can sign
  in.
- **Maintenance mode is global**, not per poll. The request describes taking "die Seite" into
  maintenance; a per-poll variant would be a different feature.
- **The maintenance notice is fixed text**, not an operator-authored message. A custom message is a
  reasonable later addition and is not assumed here.
- **Retention behaviour is unchanged.** Imported and restored polls expire by the same rule as any
  other. Nothing in this feature extends a poll's life beyond the rule that produced its deadline.
- **The export format is unchanged.** The JSON export stays tokenless, and this feature reads the
  version it currently produces rather than altering what the export writes.
- **Neither upload is size-limited.** A JSON file is caught after parsing by the content limits of
  FR-014; a backup file is opaque and is caught by nothing before FR-019 inspects it, which it can
  only do once the whole file has arrived. The accepted consequence is that an oversized upload can
  exhaust the volume. It cannot cost data, because FR-020 and FR-021 hold the existing data until a
  restore has succeeded. This is a decision, not an omission — a size check should not be added
  back as though it were one.
- **The manual restore route remains.** Replacing the file with the container stopped, as the README
  documents, stays valid and stays the fallback if the system cannot be reached at all.
