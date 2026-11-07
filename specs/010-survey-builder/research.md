# Research: Individuelle Formulare (Custom Form Builder)

**Feature**: 010-survey-builder | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

Fourteen decisions, each one either resolving something the spec left to planning or naming a
framework default that would otherwise produce the wrong behaviour silently. Every entry follows
the same shape: Decision, Rationale, Alternatives considered.

## R-1: Responses are stored relationally, one row per answered field

**Decision**: `FormResponse` holds one row per submission; `FormFieldValue` holds one row per
field the participant actually answered, `(ResponseId, FieldId)` unique, `Value` a single text
column carrying the answer in a canonical, culture-invariant textual form.

**Rationale**: FR-009 requires that deleting a field deletes every value collected for it, and
FR-042a requires deleting a single response without touching any other. Both are single `DELETE`
statements against a foreign key when values are their own rows — `DELETE FROM FormFieldValue
WHERE FieldId = @id` and `DELETE FROM FormResponse WHERE Id = @id` respectively, each backed by an
index. It also matches how this codebase already models "children of a submission": `WishItem`
and `Claim` are the same shape, one row per concrete thing rather than a document.

**Alternatives considered**: A single JSON column on `FormResponse` holding `{fieldId: value}`
was rejected — deleting one field's values would require reading and rewriting every response row
that ever answered it, an operation with no natural index, against a table this feature's own
documented scale puts at up to 200,000 rows (FR-047a). A wide table with one column per field type
(`TextValue`, `IntValue`, `DecimalValue`, `BoolValue`) was rejected because a form's fields are
open-ended (up to 50, FR-011a) while a response only ever has values for the fields that existed
and were answered — a wide table would need one join anyway to know which columns apply, with no
saving over the row-per-value shape.

## R-2: Field type is a string-backed enum, not a lookup table

**Decision**: `FieldType { Text, Integer, Decimal, Boolean, Email, Phone, PostalCode }`, stored via
EF Core's `HasConversion<string>()`, giving readable values in the SQLite file and in
`sqlite3 .dump` output used for support and debugging.

**Rationale**: FR-003 fixes the type system at exactly seven values with no configurability
requested anywhere in the spec. A C# enum is the simplest thing that satisfies it exhaustively —
the compiler enforces the switch in every place that branches on type (validation, export).

**Alternatives considered**: A `FieldTypes` lookup table with a foreign key was rejected under
Principle III — it is machinery for a set of values this feature does not intend to grow at
runtime; adding an eighth type is a code change (a new spec) regardless of which storage shape is
chosen. An `int` enum was rejected in favour of the string conversion specifically so the database
itself stays readable without cross-referencing an enum definition.

## R-3: No owner column, no `OwnerScope` extension

**Decision**: `Form` carries no `CreatorId`. Every form-building and response-viewing route lives
under the existing `admin` group, authenticated exactly like `WishListAdminEndpoints`.

**Rationale**: The spec's clarification session settled this directly — feature 009's `OwnerScope`
(`Creators/OwnerScope.cs`) exists specifically so a later feature can extend the closed capability
list (FR-028c) *if it chooses to*; this feature does not choose to (FR-032). Not adding the column
now is what Principle III's "introduced in response to a second concrete use, not in anticipation
of one" actually asks for — a nullable `CreatorId` added "for later" would be exactly the
speculative column that principle rejects, with no second use in this feature to justify it.

**Alternatives considered**: Adding the nullable `CreatorId` now, unused, "to save a migration
later" — rejected: it is anticipatory, it grows `OwnerScope` with no caller, and every index and
Fluent API line CLAUDE.md flags as easy-to-break for the *existing* Creator→content relationship
would need to be replicated for a relationship this feature does not create. Extending
`OwnerScope` with a `Forms()` method now — rejected for the same reason; there is no creator route
that would call it.

## R-4: Field order is a dense integer, renumbered on every change

**Decision**: `FormField.DisplayOrder` is a plain `int`, `0..N-1` within one form, contiguous.
Adding a field appends at `N`; removing a field renumbers the remainder down by one; reordering
replaces the whole sequence in one transaction from a client-supplied ordered id list.

**Rationale**: FR-011a caps a form at 50 fields, so a full renumber touches at most 50 rows — an
operation with no measurable cost. Dense integers keep `ORDER BY DisplayOrder` trivial and the
export order (FR-010) exactly reproducible with no secondary sort key needed.

**Alternatives considered**: Fractional or gap-based ordering (inserting at `1.5` between `1` and
`2`) was rejected as solving a problem — frequent single-item reinsertion at scale — this feature
does not have; at 50 items a full renumber is cheaper than the float-precision and gap-exhaustion
handling that scheme requires, and would be exactly the kind of premature optimisation Principle
III warns against.

## R-5: CSV is hand-written; no CSV library is added

**Decision**: A small internal writer applies RFC 4180 quoting (wrap in double quotes and double
any interior quote whenever a value contains a comma, a quote or a line break) over a flat table:
one column per field, one row per response, in `FormField.DisplayOrder` order.

**Rationale**: Grepping the backend for any existing CSV handling returns nothing — `PollExport.cs`
(feature 003) is JSON-only, so there is no existing CSV precedent to extend and no existing
dependency to reuse. The table this feature exports has no nesting, no multi-line cells beyond
free text, and a small, fixed set of quoting rules; writing it directly is a few dozen lines.

**Alternatives considered**: `CsvHelper` (the standard .NET CSV package) — rejected under
Principle III's "a new dependency MUST be justified against the cost of writing the needed
behavior directly; convenience alone is not sufficient justification." The behaviour needed —
quote-if-necessary, one row per response — does not reach the complexity CsvHelper's configuration
surface is built for (custom converters, streaming large files, culture-specific formats).

## R-6: The builder's drag-and-drop is native HTML5 drag events, no new dependency

**Decision**: `FormBuilderView.vue` implements reordering with the browser's own
`draggable="true"` attribute and `dragstart`/`dragover`/`drop` handlers, alongside explicit
move-up/move-down buttons per field.

**Rationale**: `frontend/package.json` carries no drag-and-drop library today (no `vuedraggable`,
no `sortablejs`) — confirmed by inspection. FR-049 already requires a non-drag, keyboard-operable
way to reorder a field regardless of how dragging is implemented, so a library's main selling
point — touch and keyboard support baked in — is not what closes that requirement; the explicit
buttons do. At a 50-field ceiling (FR-011a) there is no virtualised-list or performance concern a
library would be earning its weight against.

**Alternatives considered**: `vuedraggable` (a Vue wrapper over SortableJS) — rejected under
Principle III as a dependency whose main benefit (smoother drag physics, touch handling) is
convenience, not a capability the feature cannot deliver otherwise, and FR-049's required button
alternative means the drag path is never the *only* way to reorder regardless.

## R-7: Client and server validation share a rule table, not a code path

**Decision**: The seven type-check rules (FR-023 to FR-029) are written twice — once as a small
pure TypeScript module the participant form imports, once as a static C# class the server calls —
and a single JSON fixture of `{type, value, expectedValid}` cases is consumed by both a Vitest
test and an xUnit test, so a rule that drifts between the two languages fails a test on the side
that drifted.

**Rationale**: FR-030 requires the two checks to never disagree. Sharing one executable
implementation across TypeScript and C# is not available without a runtime bridge (WASM, a
generated parser) that would be new infrastructure for seven regular-expression-shaped rules.
Sharing a **test fixture** instead catches disagreement — the actual risk FR-030 names — without
adding a build step.

**Alternatives considered**: A shared JSON Schema (or similar declarative format) validated by a
library on both sides — rejected: none of the seven rules need JSON Schema's expressiveness, and
it would add a dependency on both sides for rules simple enough to write directly (Principle III).
Generating the TypeScript rules from the C# source (or vice versa) — rejected as build-pipeline
machinery for seven functions that will not change independently of their own test suite.

## R-8: Form-building lives under `/admin`; only submission and fetch are public

**Decision**: `FormAdminEndpoints.cs` (create, edit, reorder, delete, list responses, delete one
response, export) is mapped on the existing `admin` group, exactly like
`WishListAdminEndpoints.cs`. `FormEndpoints.cs` (fetch a form's definition, submit a response) is
mapped beside `PollEndpoints`/`WishEndpoints` on the plain `api` group, under a new one-letter
prefix, `/f/{formToken}`.

**Rationale**: `MaintenanceMiddleware` excludes `/api/v1/admin/**` (confirmed in
`Maintenance/MaintenanceMiddleware.cs`) so that a restore in progress does not race an operator's
own admin actions — the same reason every other admin CRUD surface lives there. Since this
feature is operator-only (R-3), there is no token-scoped surface like `/e/{creatorToken}` that
needs to sit *outside* `/admin` for maintenance reasons the way feature 009's creator surface did;
the only route that must be maintenance-gated is the participant-facing one (FR-045), and it earns
that by living beside `/u` and `/w`, not by any special-casing.

**Alternatives considered**: Putting form-building under a new top-level `/forms` group outside
`/admin` — rejected: it would need its own `.RequireAuthorization()` wiring and would lose the
maintenance-mode exemption every other admin surface already has for free, for no benefit, since
there is no token-holder this feature needs to keep separate from the operator (contrast with 009,
where the creator surface's placement *had* to be deliberate because a bearer token was involved).

## R-9: Submissions reuse the existing 10/hour participant rate-limit policy

**Decision**: `POST /api/v1/f/{formToken}/responses` carries `.RequireRateLimiting
(RateLimiting.SubmissionPolicy)` — the same named policy `WishEndpoints` and `PollEndpoints`
already apply to their own writes. No new policy is added.

**Rationale**: FR-022 asks for the submission route to be rate-limited "on the same terms as
participant submissions elsewhere in the system," and one participant fills in and submits a form
exactly once (unlike an Ersteller building a multi-item wish list, which is why feature 009 needed
a *different* number, `CreatorWritePolicy` at 60/hour). Reusing the existing policy is not just
simpler, it is the literal requirement.

**Alternatives considered**: A new `FormSubmissionPolicy` at some other number — rejected; nothing
in the spec or the shape of the task (one submission) suggests ten per hour is wrong for it, and
inventing a new number without a reason would be exactly the "speculative configurability"
Principle III forbids.

## R-10: Reading a form's definition is not rate-limited

**Decision**: `GET /api/v1/f/{formToken}` carries no rate-limit attribute, matching
`WishEndpoints`' own read route.

**Rationale**: FR-012 requires no step of any kind between the link and the form; a read limit
that could refuse the very first load of a shared link would put a step there. Every other
participant-facing read in this codebase is already unlimited — this is consistency, not a new
decision.

## R-11: The restore preview gains form and response counts via the same tolerant-read pattern feature 009 established

**Decision**: `RestorePreview` gains `FormsInBackup`, `ResponsesInBackup`, `FormsLost`,
`ResponsesLost`. A new `ReadFormCountsAsync(connection, ct)` in `RestoreService.cs` checks
`sqlite_master` for the `Forms`/`FormResponses` tables before counting, returning `(0, 0)` when
they are absent — the exact shape `ReadCreatorCountAsync` already uses for the Ersteller counts
009 added.

**Rationale**: FR-047 requires the preview to count what a restore would destroy, the same defect
008's research (R-10) recorded for wish lists and 009 repeated the fix for. The tolerant check
matters for one concrete case: an operator restoring a backup taken *before* this feature shipped
must not see the restore fail because the backup's schema predates the `Forms` table.

**Alternatives considered**: None seriously — this is the established pattern in this exact file,
used twice already; deviating from it would be the new risk, not a choice with a real trade-off.

## R-12: The whole-file backup needs no code change

**Decision**: Nothing in `BackupService.cs` changes. FR-046 is satisfied automatically once the
migration exists, because `BackupService.CreateAsync` copies the entire SQLite file via the
engine's own online-backup API — it has no per-table knowledge to update.

**Rationale**: Stated for completeness, since FR-046 reads like it might need dedicated work; it
does not, and the plan should say so rather than leave a reader wondering whether it was missed.

## R-13: A required boolean field's "unanswered" state is the absence of a row, not a third stored value

**Decision**: A boolean field's value is only ever `true` or `false` in `FormFieldValue.Value`
when the participant chose one. "Not yet answered" is simply the absence of a `FormFieldValue` row
for that field in that response — the same rule every other optional-and-unanswered field already
follows in this schema. The participant control is a `v-radio-group` (or `v-btn-toggle`) with
**no** option pre-selected, never a `v-checkbox`, whose two states (checked/unchecked) cannot
represent "unanswered" at all.

**Rationale**: The spec's third clarification-adjacent requirement, FR-015, needs a yes/no field
to have a distinguishable unanswered state so that marking it required has an effect. Because
every field's requiredness is already checked the same way — "does a value row exist for this
field in this response" — a boolean field needs no special schema: the UI control choice is what
does the work, not a third database value like `null`/`unset`.

**Alternatives considered**: A nullable `bool?` column with three states (`true`/`false`/`null`)
on a dedicated boolean-typed column — rejected together with the wider-table idea in R-1; it would
special-case one field type's storage while every other type already gets "unanswered" for free
from row absence.

## R-14: Export order and missing values follow directly from the schema; no extra logic needed

**Decision**: Both exports query `FormField` ordered by `DisplayOrder`, then for each
`FormResponse` left-join `FormFieldValue`. A response with no row for a given field (because the
field was added after that response was submitted, FR-038) naturally produces an absent CSV cell
or JSON property — no conditional code is written to detect and special-case this.

**Rationale**: FR-010 and FR-038 are both direct consequences of the query shape once R-1 and R-4
are in place; calling this out here is to record that it is not additional work, so it is not
mis-estimated in `tasks.md`.

One genuinely new rule, because nothing about it follows from the schema: the spec allows two
fields to share a label (FR-004 does not require labels to be distinct), but JSON export keys a
response's values by label (FR-035). Two fields named "Name" would collide as one JSON key. The
export layer disambiguates by appending " (2)", " (3)", … to every label after the first
occurrence of a duplicate, in field order — a small, local rule with no schema consequence, applied
only at export time (contracts/openapi.yaml, `FormExportDocument`).
