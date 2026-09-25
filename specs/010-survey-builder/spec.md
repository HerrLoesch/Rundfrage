# Feature Specification: Individuelle Formulare (Custom Form Builder)

**Feature Branch**: `010-survey-builder`
**Created**: 2026-09-22
**Status**: Draft
**Input**: User description: "Erweitere die Webseite um die Möglichkeit Umfragen zu erstellen. Am
liebsten wäre mir ein Editor ähnlich wie bei google forms. Wo man sich die UI per Drag and Drop
zusammen stellen kann. Das Ergebnis soll man wahlweise als CSV oder Json herunterladen können.
Wichtig ist auch, dass man für die Einzelnen bereiche des Formulars typische Datentypen wie
emails, telefonnummern, postleitzahlen, Zahlen, Texte (mit Längenangabe), wahrheitswerte,
Fließkommazahlen, Ganzzahlen, angeben kann. Man soll auch eingabe bereiche als Benötigt bzw.
optional kennzeichnen können. Damit sich die Eingabe nur abschicken lässt, wenn man alle
benötigten Werte eingegbene hat."

## Summary

Rundfrage currently offers two fixed shapes of survey: a date poll, which asks "which of these
days work", and a wish list, which asks "which of these items do you want". Neither lets the
operator ask an arbitrary question of their own choosing, typed and constrained the way they need
it. This feature adds a third, general-purpose shape: a **Formular** (form) the operator builds
field by field — arranging typed questions on a canvas the way Google Forms lets an author arrange
theirs — and shares as one link. A participant reaches that link with no account, answers every
field the operator marked required, and cannot submit until they have. The operator later takes
the answers out as a CSV file or a JSON file, whichever suits what they are doing with them next.

Three properties define the feature and each one is deliberate.

**The field, not the form, carries the type.** A form is an ordered list of fields, and each field
is one of seven kinds — short text with a length limit, whole number, decimal number, yes/no,
email address, phone number, postal code — chosen once when the field is added and enforced on
every submission the field ever receives. This is what lets one operator collect a phone number
and another collect a five-digit code from the same building block.

**Building the form is a canvas, not a wizard.** The operator sees the fields they have added as a
list they can reorder by dragging, add to at either end or in the middle, and remove — the same
direct-manipulation shape Google Forms uses, chosen because a linear step-by-step wizard cannot
show the operator the form as a whole while they build it.

**Requiredness is enforced twice, not once.** The participant's browser refuses to submit while a
required field is empty or malformed, so nobody wastes a round trip on a mistake they can see; the
server refuses the same submission again, because a browser is not a boundary anyone can trust.
Both checks exist and neither substitutes for the other.

## Clarifications

### Session 2026-09-22

- Q: What documented scale should forms, fields and responses target? → A: **Medium** — mirrors
  feature 008's wish-list scale: at most 50 fields per form (enforced); a documented,
  non-enforced installation-wide scale of 200 forms and 200,000 responses (FR-011a, FR-047a,
  SC-009).
- Q: Can the operator delete a single response, not just the whole form? → A: **Yes** — with an
  explicit confirmation, leaving the form, its fields and its other responses unaffected, matching
  the precedent set by date polls (002 FR-037a, FR-038) and wish lists (008 FR-038) (FR-042a,
  SC-006a).
- Q: Can a field's type be changed after it is created? → A: **No** — type is fixed at creation;
  changing it means deleting the field and adding a new one. Reinterpreting values already
  collected under a new type would leave them with no defined meaning (FR-008).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build a form by dragging fields into place (Priority: P1)

The operator opens the forms area, starts a new form, and gives it a title. They add a text field
for "Name" with a length limit, an email field, a yes/no field for "Kommst du zum Essen?", and a
whole-number field for "Wie viele Personen?". They drag the email field above the text field
because they want it asked first. They mark the name and the email required, leave the headcount
optional, and share the one link the form produces.

**Why this priority**: This is the feature. Without a working builder there is nothing to share
and nothing to answer; every other story depends on a form existing first.

**Independent Test**: Build one form with at least four fields covering at least three different
types using only drag-and-drop and the add/remove controls, reorder two of them, and confirm the
published field order matches the order left in the builder.

**Acceptance Scenarios**:

1. **Given** the operator is signed in, **When** they start a new form and give it a title, **Then**
   the form is created empty and the builder opens on it.
2. **Given** an empty form, **When** the operator adds a field and chooses one of the seven field
   types, **Then** the field appears in the builder with that type and an editable label.
3. **Given** a form with several fields, **When** the operator drags one field to a new position,
   **Then** the field list reorders immediately and the new order is what participants and exports
   will see.
4. **Given** a field in the builder, **When** the operator removes it, **Then** it disappears from
   the builder and will no longer be asked of participants.
5. **Given** a text field, **When** the operator adds it, **Then** they must set a maximum length
   before it can be saved, and may optionally set a minimum length.
6. **Given** any field, **When** the operator toggles it between required and optional, **Then**
   the builder shows which state it is in without relying on colour alone.
7. **Given** a form with at least one field, **When** the operator asks for its link, **Then**
   exactly one participant link is produced, containing an unguessable token.
8. **Given** a form with no fields, **When** the operator looks for its link, **Then** none is
   offered, and the builder explains that a field must be added first.

---

### User Story 2 - A participant answers and cannot submit something incomplete (Priority: P1)

A participant opens the link on their phone. They see the fields in the order the operator left
them, each rendered for its type — a text box, a yes/no choice, a number pad for the headcount.
Required fields are marked. They type an email address with no "@" and try to submit: the page
tells them which field is wrong before anything is sent. They fix it, leave the optional headcount
blank, and submit. The confirmation tells them it worked.

**Why this priority**: This is the other half of the feature and the one the request states
explicitly — "damit sich die Eingabe nur abschicken lässt, wenn man alle benötigten Werte
eingegeben hat." A builder that produces a form nobody can safely fill in is worthless.

**Independent Test**: Open a published form with a mix of required and optional fields with no
account; attempt to submit with a required field empty and with a required field holding a value
of the wrong shape; confirm both are blocked with the offending field identified; then submit a
valid response and confirm it is recorded.

**Acceptance Scenarios**:

1. **Given** a published form, **When** it is opened with no account and no session, **Then** every
   field is shown in the operator's order with no step between the link and the form.
2. **Given** a required field left empty, **When** the participant tries to submit, **Then**
   submission is blocked, the field is identified, and nothing is sent to the server.
3. **Given** a field holding a value that does not match its type — text over its length limit, a
   non-numeric value in a number field, a malformed email, phone number or postal code, **When**
   the participant tries to submit, **Then** submission is blocked and the field is identified.
4. **Given** an optional field left empty, **When** every required field is valid, **Then**
   submission succeeds without the optional field.
5. **Given** a submission that bypasses the browser's checks entirely, **When** it reaches the
   server missing a required field or holding a malformed value, **Then** the server refuses it,
   names the field, and stores nothing from it.
6. **Given** a required yes/no field, **When** the participant has not yet chosen either answer,
   **Then** it counts as unanswered — the field does not default to "no" or "yes" on their behalf.
7. **Given** a valid, complete submission, **When** it is sent, **Then** the participant sees a
   confirmation and the response is stored exactly as entered.
8. **Given** a published form, **When** several different participants open the same link, **Then**
   each may submit their own response; the form does not require or track who they are.

---

### User Story 3 - Take the answers out as CSV or JSON (Priority: P2)

Weeks later the operator opens the form in the admin area, sees how many responses have come in,
and downloads them — as a CSV file to open in a spreadsheet, or as a JSON file to feed into
another tool. Either file has one row or object per response, one column or property per field, in
the order the fields were left in the builder, with numbers and yes/no values typed correctly
rather than turned into text.

**Why this priority**: The request asks for the choice explicitly — "wahlweise als CSV oder Json."
It ranks below building and answering because an export with nothing collected yet has nothing to
prove, but no form is useful if what it collects cannot leave the system.

**Independent Test**: Collect several responses on one form covering every field type, download
both formats, and confirm each file names every field and reproduces every response's values with
the correct type and in field order.

**Acceptance Scenarios**:

1. **Given** a form with responses, **When** the operator downloads it as CSV, **Then** the file
   has one header per field in builder order and one row per response.
2. **Given** a form with responses, **When** the operator downloads it as JSON, **Then** the file
   has one entry per response, each holding every field's value keyed by its label, with numbers
   as numbers and yes/no as a boolean, not as text.
3. **Given** a form with zero responses, **When** the operator downloads either format, **Then**
   they receive a valid, empty file rather than an error.
4. **Given** a form whose fields were reordered after some responses were already collected,
   **When** it is exported, **Then** every response — old and new — is presented in the field order
   currently set in the builder.
5. **Given** a form the operator does not own is requested by identifier, **When** any other
   operator session attempts to export it, **Then** authentication is required exactly as it is for
   viewing or editing any other admin content.

---

### User Story 4 - Manage the set of forms (Priority: P3)

The operator opens the forms area and sees every form they have built: its title, how many fields
it has, how many responses have come in, and whether it currently has a working link. They rename
one, edit the fields of a form that has already collected responses, and delete one that is no
longer needed, which removes it and every response to it.

**Why this priority**: The first three stories work with a single form already in hand. This story
is what makes the feature usable once more than one form exists side by side.

**Independent Test**: Build two forms, collect a response on each, edit one form's fields, delete
the other, and confirm the deleted form's link and responses are gone while the edited form's
existing responses and new field set are both correct.

**Acceptance Scenarios**:

1. **Given** the forms area, **When** it is opened, **Then** every form is listed with its title,
   field count, response count and link status.
2. **Given** a form with existing responses, **When** the operator adds a new field to it, **Then**
   existing responses are unaffected and have no value for the new field.
3. **Given** a form with existing responses, **When** the operator removes a field from it, **Then**
   the field and every value already collected for it are deleted, and the form's other responses
   are otherwise unchanged.
4. **Given** a form, **When** the operator renames it, **Then** the new title is shown everywhere
   the form appears and its link, fields and responses are unaffected.
5. **Given** a form, **When** the operator deletes it, **Then** an explicit confirmation states how
   many responses will be destroyed, and on confirming, the form, its fields and every response are
   removed and its participant link behaves as an unknown link.
6. **Given** a form with several responses, **When** the operator deletes one response, **Then**
   only that response is removed, with an explicit confirmation beforehand, and the form, its
   fields, its link and its other responses are unaffected.
7. **Given** no forms exist, **When** the operator opens the forms area, **Then** it says so and
   offers building the first one directly from that state.

---

### Edge Cases

- **A required field is added to a form that already has responses.** Responses collected before
  the change keep whatever they hold; the field's requiredness applies only to submissions made
  after the change.
- **A form is deleted while a participant has it open, partway through filling it in.** Their
  submission is refused exactly as a submission to any other deleted survey is refused today.
- **A postal code or phone number is technically well-formed but does not correspond to a real
  place or line.** The system checks shape, not existence; validating that a code or number is real
  is out of scope.
- **A text field's maximum length is set very low or very high.** Both are accepted; the field
  simply refuses input past whatever limit the operator chose.
- **Two participants submit at nearly the same moment.** Both are recorded independently; nothing
  about a form limits it to one response.
- **The operator reorders fields, then immediately exports.** The export reflects the order at the
  moment of export, not the order at the moment any individual response was collected.
- **Maintenance mode is switched on.** Answering a form is refused with the same notice the other
  participant surfaces already show; existing responses are unaffected. Building and editing a
  form is unaffected, exactly as editing a date poll or wish list is unaffected today — these are
  operator actions under `/admin`, not routes maintenance mode gates.
- **A restore replaces the installation.** The forms, fields and responses in the restored file are
  what exist afterward; anything created after the backup was taken is gone, exactly as for date
  polls and wish lists.
- **A field is removed and a same-typed field with the same label is added back later.** It is a
  new field with no responses; nothing is recovered from the one that was deleted.
- **The builder is used on a narrow phone screen.** Dragging a field to reorder it must still be
  possible, or an equivalent non-drag way of reordering must be offered, since the constitution's
  accessibility bar applies to the builder as much as to any other control.

## Requirements *(mandatory)*

### Functional Requirements

#### The form and its fields

- **FR-001**: The operator MUST be able to create a new form with a title. A title MUST be given;
  creation without one MUST be refused, naming what is missing.
- **FR-002**: A form MUST consist of an ordered list of fields the operator composes by adding,
  removing and reordering them; the list MAY be empty.
- **FR-003**: Adding a field MUST require choosing exactly one of seven types: short text, whole
  number, decimal number, yes/no, email address, phone number, postal code.
- **FR-004**: Every field MUST have a label, written by the operator, shown to participants as the
  question they are answering.
- **FR-005**: Every field MUST be marked required or optional by the operator, defaulting to
  optional when a field is first added.
- **FR-006**: A short-text field MUST have a maximum length set by the operator at the time it is
  added, and MAY have a minimum length. Editing either afterward (FR-008) MUST keep the pair
  consistent — a minimum MUST NOT exceed the maximum — and the maximum MUST stay within the
  system's bound (data-model.md §2). Shrinking a limit below the length of an answer already
  collected under it is permitted: the requirement governs what a *future* submission may hold,
  not the values a past one already holds.
- **FR-007**: The operator MUST be able to reorder fields by dragging them into a new position; the
  builder MUST reflect the new order immediately.
- **FR-008**: The operator MUST be able to edit an existing field's label, required flag and
  length limits (for text fields) at any time, including after the form has collected responses.
  A field's type MUST NOT be changed after it is created; changing the kind of answer a question
  collects requires deleting the field (FR-009) and adding a new one.
- **FR-009**: The operator MUST be able to remove a field from a form at any time. Removing a field
  MUST delete every value already collected for it, alongside the field itself.
- **FR-010**: The order fields are left in the builder MUST be the order they are shown to
  participants and the order they appear in every export, for every response regardless of when it
  was collected.
- **FR-011**: A form's participant link MUST NOT be offered while the form has zero fields; the
  builder MUST state that a field is needed first.
- **FR-011a**: A form MUST NOT hold more than 50 fields. Adding a 51st field MUST be refused,
  stating the limit and how many the form already has.

#### The participant flow

- **FR-012**: Opening a valid form link MUST require no account, no sign-in, no password and no
  step of any kind between the link and the form (Principle I).
- **FR-013**: The participant surface MUST render every field in the form's current order, using an
  input appropriate to its type.
- **FR-014**: A required field MUST be visibly marked as required in a way that does not rely on
  colour alone.
- **FR-015**: A yes/no field MUST offer a distinguishable "not yet answered" state in addition to
  yes and no, so that marking it required has an effect; it MUST NOT default to either answer on
  the participant's behalf.
- **FR-016**: The participant's browser MUST prevent submission while any required field is empty
  or holds a value that does not match its type's format, and MUST identify which field is at
  fault without sending the incomplete submission to the server.
- **FR-017**: The server MUST independently validate every submission — every required field
  present, every field's value matching its type's format — regardless of what the participant's
  browser already checked, and MUST refuse a submission that fails, naming the field at fault.
- **FR-018**: A refused submission MUST NOT be stored in whole or in part.
- **FR-019**: A stored response MUST hold exactly the values the participant entered for the
  fields that existed at submission time, and no other participant-identifying information
  (Principle IV).
- **FR-020**: A form's link MUST accept submissions from any number of participants; the system
  MUST NOT require, collect or check who is submitting, and MUST NOT limit a form to one response.
- **FR-021**: An unknown, malformed or deleted form link MUST produce one response that does not
  distinguish between those cases (mirrors 002 FR-027, 008 FR-024, 009 FR-008).
- **FR-022**: Submissions through a form link MUST be rate-limited per request source on the same
  terms as participant submissions elsewhere in the system (002 FR-027), and a refusal MUST say
  that too many submissions were sent and that it can be retried later, without having stored
  anything.

#### Type validation

- **FR-023**: A short-text value MUST be refused if it is shorter than the field's minimum length
  (when set) or longer than its maximum length.
- **FR-024**: A whole-number value MUST be refused unless it parses as an integer with no
  fractional part.
- **FR-025**: A decimal-number value MUST be refused unless it parses as a number, fractional part
  allowed.
- **FR-026**: A yes/no value MUST be refused unless it is exactly one of "yes" or "no"; an
  unanswered optional yes/no field MUST be accepted as absent.
- **FR-027**: An email-address value MUST be refused unless it has the shape of an email address
  (a local part, an "@", a domain with at least one ".").
- **FR-028**: A phone-number value MUST be refused unless it consists of digits with optionally a
  leading "+" and interior spaces, dashes or parentheses, and at least seven digits in total. This
  is a shape check, not a directory lookup.
- **FR-029**: A postal-code value MUST be refused unless it is exactly five digits, matching German
  postal code format, since the interface is German-only (FR-039).
- **FR-030**: Every type check in this section MUST be applied identically on the participant's
  browser (FR-016) and on the server (FR-017); the two MUST never disagree about whether a value is
  valid.

#### Access and ownership

- **FR-031**: Creating, editing, publishing, reordering and deleting a form, its fields, and
  viewing or exporting its responses MUST require an authenticated operator session.
- **FR-032**: This feature grants no new capability to an Ersteller link (feature 009). A form is
  reachable and manageable only through the operator's own session; FR-028c of feature 009 leaves
  it out of the creator surface unless a future feature explicitly extends it.
- **FR-033**: Because there is exactly one operator account (Principle I; no multi-operator
  concept exists in this system), there is no "belongs to a different operator" case to guard
  against — FR-031's session requirement is the whole of this feature's access control. An
  unauthenticated request naming a real formId and one naming a fake one MUST still receive the
  identical 401, so that a session is never something a probe can shortcut by guessing (mirrors
  002 FR-002's broader indistinguishability principle, applied here to authentication rather than
  ownership).

#### Export

- **FR-034**: The operator MUST be able to download a form's responses as a CSV file.
- **FR-035**: The operator MUST be able to download a form's responses as a JSON file.
- **FR-036**: Both export formats MUST include every field currently on the form, in the form's
  current order, and MUST include every stored response, one row (CSV) or one object (JSON) per
  response.
- **FR-037**: Exported values MUST preserve their type: whole and decimal numbers as numbers and
  yes/no as a boolean in JSON; CSV MUST render them in a form a spreadsheet reads as the correct
  type, not as arbitrary text.
- **FR-038**: A response holding no value for a field that was added after that response was
  submitted MUST export that value as absent (empty in CSV, `null` or omitted in JSON), never as a
  guessed default.
- **FR-039**: Exporting a form with zero responses MUST produce a valid file containing the field
  headers or an empty list, not an error.

#### Form lifecycle and retention

- **FR-040**: The forms area MUST list every form the operator has built, with its title, field
  count, response count and whether it has a working link.
- **FR-041**: The operator MUST be able to rename a form at any time without affecting its link,
  fields or responses.
- **FR-042**: The operator MUST be able to delete a form. Deletion MUST require an explicit
  confirmation stating how many responses will be destroyed, and on confirming MUST destroy the
  form, its fields and every response; its participant link MUST afterward behave as an unknown
  link.
- **FR-042a**: The operator MUST be able to delete a single response from a form, with an explicit
  confirmation, leaving the form, its fields, its link and its other responses unaffected (mirrors
  002 FR-037a/FR-038, 008 FR-038).
- **FR-043**: A form MUST have no automatic expiry. It persists until the operator explicitly
  deletes it (Principle IV's requirement for a defined retention outcome is met by an explicit
  deletion path, the same choice feature 008 made for wish lists, since a form has no date-based
  natural end the way a date poll does).
- **FR-044**: When no forms exist, the forms area MUST say so and offer building the first one
  directly from that state, distinguishing "none exist" from "the stored data cannot be read right
  now" (mirrors 007 FR-017).
- **FR-045**: While maintenance mode is on, the participant-facing route that fetches a form or
  submits a response to it MUST be refused with the notice the other participant surfaces already
  use (feature 005). Building, editing, reordering and deleting a form are session-authenticated
  operator actions under `/admin`, and — exactly like every other admin surface (002, 008) —
  remain reachable during maintenance mode; only the installation-wide controls maintenance mode
  itself gates (backup, restore, settings) are off-limits there, and this feature adds none of
  those.
- **FR-046**: The whole-installation backup MUST include every form, its fields and its responses,
  so a restore returns the installation to a state in which the same form links work.
- **FR-047**: The restore preview MUST count the forms and responses it will destroy alongside the
  polls, wish lists and Ersteller it already counts (mirrors 009 FR-052, the defect 008 recorded
  for wish lists).
- **FR-047a**: The documented scale for this feature is up to 200 forms and up to 200,000
  responses, installation-wide — the same order of magnitude as feature 008's wish-list scale
  (200 wish lists, 200,000 entries). Unlike the 50-field cap of FR-011a, this is a documented
  scale, not an enforced maximum: the system MUST remain correct beyond it, with only the
  two-second performance budget of SC-009 ceasing to apply (007 FR-028c, 008 FR-054).

#### Presentation and accessibility

- **FR-048**: All text introduced by this feature MUST come from the translation catalogue; German
  remains the only interface language (007 FR-035).
- **FR-049**: Every interactive control in the builder and on the participant surface MUST be
  operable by keyboard alone, MUST carry a text label naming what it does, and MUST visibly mark
  keyboard focus — including reordering a field, for which a non-drag alternative (such as move-up
  and move-down controls) MUST also be offered (002 FR-050 to FR-052).
- **FR-050**: The builder and the participant surface MUST remain usable on a screen 375 pixels
  wide.

### Key Entities

- **Formular (Form)**: An operator-owned survey built from an ordered list of fields. Attributes:
  title, creation date, ordered fields (at most 50, FR-011a), at most one current participant link
  token, response count. Has no automatic expiry; exists until deleted.
- **Feld (Field)**: One question on a form. Attributes: type (one of the seven listed in FR-003,
  fixed for the field's lifetime), label, required flag, and — for short-text fields — a maximum
  length and optional minimum length. Belongs to exactly one form and has a position in that
  form's order. Deleting a field deletes every value collected for it.
- **Antwort (Response)**: One participant's complete submission to a form at one point in time:
  one value per field that existed when it was submitted, and a submission timestamp. Carries no
  participant identity, IP address or user-agent (Principle IV).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator with no prior use of the builder can create a form with at least five
  fields spanning at least four of the seven types, using only drag-and-drop and the add/remove
  controls, in under five minutes.
- **SC-002**: Across a test battery exercising every field type with both a valid and an invalid
  value, 100% of invalid values are blocked before a request reaches the server, and 100% of the
  same invalid values are also refused when sent directly to the server.
- **SC-003**: Zero responses are stored anywhere in the system as a result of a rejected
  submission, checked across every rejection reason FR-016 through FR-029 define.
- **SC-004**: A participant can complete and submit an eight-field form on a screen 375 pixels wide
  in under two minutes, entirely by touch or by keyboard.
- **SC-005**: For a form with responses collected before and after two separate field-order
  changes, both the CSV and the JSON export show 100% of responses in the form's current field
  order, and every value's type in the export matches the field's declared type.
- **SC-006**: Deleting a form leaves zero of its fields, zero of its responses and zero working
  copies of its link; its response count cannot be produced by any export or listing afterward.
- **SC-006a**: Deleting a single response leaves the form's other responses, fields and link fully
  intact: the response count decreases by exactly one and zero other responses change.
- **SC-007**: Reordering fields by dragging updates the field order shown to the next participant
  within one page load, with no server round trip required to see the new order in the builder.
- **SC-008**: A participant can open a form link and submit a complete, valid response in a single
  session with zero sign-up steps, in 100% of trials.
- **SC-009**: At the documented scale of 200 forms and 200,000 responses installation-wide, and
  with any single form holding up to 50 fields, the forms area and a form's response list each
  appear within two seconds of being opened.

## Assumptions

- **The seven listed types are the whole of the type system for v1.** The request names email,
  phone number, postal code, whole number, decimal number, short text with a length, and yes/no.
  "Zahlen" (numbers) in the request is read as the general category the request immediately
  refines into whole and decimal numbers, not as an eighth, undifferentiated numeric type.
- **No choice-type questions.** Single-choice, multiple-choice and dropdown fields — a large part
  of what Google Forms offers — are not in the request's explicit list of data types and are left
  out of this iteration; the comparison to Google Forms is read as being about the drag-and-drop
  building experience, not about matching its full question catalogue.
- **One continuous page per form.** Google Forms' multi-section, multi-page forms are not
  requested and are left out; every form here is answered on one page, consistent with every other
  participant surface in the system.
- **This feature is operator-only.** Feature 009 built Ersteller links as a closed list of
  capabilities (its FR-028c) that later features must explicitly choose to extend. This feature
  makes no such choice; extending form-building to Ersteller links is left for a future feature to
  decide.
- **No response editing and no duplicate prevention.** A participant submits once and cannot return
  to change their answer, and the system does not attempt to recognise the same participant twice.
  Both follow from Principle I: there is no participant identity to attach an edit token or a
  duplicate check to without asking for one.
- **A form has no automatic expiry.** Unlike a date poll, a form has no date-based natural end, so
  it follows the wish list's model: it lives until the operator deletes it.
- **Numeric fields have no range constraint.** The request asks for a length limit on text fields
  only; whole- and decimal-number fields accept any value that parses as that type, with no
  minimum or maximum the operator can set.
- **Postal code means the German five-digit format**, since the interface and its audience are
  German-only (FR-048), matching the existing project-wide choice to serve one locale.
- **Phone number validation checks shape, not reachability.** A permissive pattern (digits, an
  optional leading "+", and spaces, dashes or parentheses between them) is enough to catch obvious
  mistakes without claiming to verify that a number is real or dialable.
- **Deleting a field deletes its collected answers.** This mirrors how deleting a wish-list item
  removes its entries (008) rather than leaving orphaned values with nothing to attach them to.

## Dependencies

- Feature 002 (date poll) supplies the unguessable capability token, the indistinguishable
  response for unknown links, the participant rate limit and the standard for client-plus-server
  validation this feature reuses for form links and submissions.
- Feature 003 (SQLite and export) supplies the export pipeline pattern this feature extends with a
  JSON option alongside the existing CSV one.
- Feature 005 (maintenance mode) supplies the state that must also refuse form building and form
  submission.
- Feature 007 (admin shell and dashboard) supplies the navigation, area, empty-state and
  error-state conventions the forms area follows.
- Feature 008 (wish list) supplies the precedent for a survey type with no automatic expiry and an
  explicit deletion path, which this feature's retention model follows.
- Feature 009 (creator links) supplies the closed-capability-list pattern (FR-028c) this feature
  relies on to stay operator-only without an explicit exclusion rule of its own.

## Out of Scope

- Single-choice, multiple-choice, dropdown, file-upload, date and any other field type not among
  the seven this spec names.
- Multi-page or sectioned forms, and any conditional logic or branching between fields.
- Ersteller access to the builder or to a form's responses (see Assumptions).
- Editing or withdrawing a submitted response after the fact.
- Preventing or detecting duplicate submissions from the same participant.
- A minimum or maximum value constraint on whole-number or decimal-number fields.
- Collaboration between multiple operators on the same form.
- Automatic expiry or scheduled deletion of a form.
