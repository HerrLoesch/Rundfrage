# UI Contract: Individuelle Formulare

**Feature**: 010-survey-builder | **Date**: 2026-09-22 | **Spec**: [spec.md](../spec.md)

What the interface must do, in terms a test can assert. Test ids are part of this contract: the
end-to-end suite addresses elements by `data-testid`, and renaming one is a breaking change.

Measurements — container widths, gaps, heading sizes, card and field styling — are **not** in this
contract. They belong to [`specs/design-system.md`](../../design-system.md), which these surfaces
follow rather than restate.

---

## 1. Navigation (007 FR-002a to FR-005)

`AdminNav.vue` gains one entry, in the middle section, after "Ersteller".

| Position | Label (catalogue key) | Route names owned | `data-testid` |
|---|---|---|---|
| 1 | `nav.dashboard` | `dashboard` | `nav-dashboard` |
| 2 | `poll.listTitle` | `polls`, `poll-answers` | `nav-polls` |
| 3 | `nav.wishLists` | `wish-lists`, `wish-list` | `nav-wish-lists` |
| 4 | `nav.creators` | `creators` | `nav-creators` |
| **5** | **`nav.forms` = "Formulare"** | **`forms`, `form-builder`** | **`nav-forms`** |
| 6 | `nav.settings` | `settings` | `nav-settings` |

- Settings stays last (007 FR-003, 009 FR-047) — unchanged by this feature.
- Unlike the Ersteller area (one route, no detail destination), a form gets a **detail address**,
  `/admin/formulare/:formId`, following 007's ordinary rule rather than the creator surface's
  single-address exception — a form is reached only with an operator session already established,
  so there is no credential in the address to protect (unlike 009 FR-028d's `/e/:creatorToken`).
- Exactly one entry carries `aria-current="page"` at any time.

## 2. Routes

| Address | Route name | Component | Session |
|---|---|---|---|
| `/admin/formulare` | `forms` | `FormsView.vue` | required |
| `/admin/formulare/:formId` | `form-builder` | `FormBuilderView.vue` | required |
| `/f/:formToken` | `form` | `FormFillView.vue` (in `BareShell`) | **none** |

`FormFillView.vue` lives in `BareShell` beside `/u/:pollToken`, `/w/:listToken` and
`/e/:creatorToken` — no navigation drawer, no sign-out, no link into `/admin` (FR-048).

No navigation guard on either admin route, for the reason `router.ts` already records: the server
is the authority.

## 3. The forms area — `FormsView.vue`

Opens on the list (mirrors 007 FR-014h). Creating is an action that reveals a form (the operator's
own creation dialog, not the builder itself), not a form that is always open; leaving the area
discards an unconfirmed entry (007 FR-014j).

| Element | `data-testid` | Requirement |
|---|---|---|
| The list | `form-list` | FR-040 |
| One row | `form-row` | FR-040 |
| Row: title | `form-title` | FR-001 |
| Row: field count | `form-field-count` | FR-040 |
| Row: response count | `form-response-count` | FR-040 |
| Row: "kein Feld — Link nicht erreichbar" state | `form-no-fields` | FR-011 |
| Row: the link, copyable (once ≥1 field) | `form-link` | FR-012 |
| Reveal the creation dialog | `form-create-open` | FR-001 |
| The creation dialog (title only) | `form-create-dialog` | FR-001 |
| Open a form's builder | `form-open` | US1 |
| Delete a form | `form-delete` | FR-042 |
| Empty state, offering creation | `form-empty` | US4 scenario 6 |
| Storage-unavailable state | `form-unavailable` | 007 FR-017 |

**States that must be distinguishable** (mirrors 007 FR-017, 009 FR-046): *no form exists* says so
in words and offers creating one; *the data cannot be read* says so and shows no list. Neither is
ever rendered as a zero.

### 3a. Deleting a form must state both what and how much (FR-042)

| | Delete (`form-delete`) |
|---|---|
| Confirmation | names the form and states the **response count** that will be destroyed |
| Confirmation testid | `form-delete-confirm` |
| Visual weight | destructive |
| After it | the row is gone |

`DeleteConfirm.vue` is reused rather than a new confirmation component written (mirrors 009's
reuse of the same component for Ersteller deletion).

The field-limit refusal (FR-011a) states the limit (50) and how many fields the form already has.
Test id `form-field-limit-refusal`.

## 4. The builder — `FormBuilderView.vue` (`/admin/formulare/:formId`)

One page: a title, the field canvas, and (once responses exist) a responses panel with the two
export buttons.

| Element | `data-testid` | Requirement |
|---|---|---|
| Title, editable inline | `form-builder-title` | FR-041 |
| The field canvas (ordered list) | `form-field-canvas` | FR-002, FR-007 |
| One field card | `form-field-card` | FR-002 |
| Field card: type icon/label (not editable) | `form-field-type` | FR-008 |
| Field card: label, editable | `form-field-label-input` | FR-004, FR-008 |
| Field card: required toggle | `form-field-required-toggle` | FR-005, FR-014 |
| Field card: max length (Text only) | `form-field-max-length-input` | FR-006 |
| Field card: min length (Text only, optional) | `form-field-min-length-input` | FR-006 |
| Field card: drag handle | `form-field-drag-handle` | FR-007; research R-6 |
| Field card: move up | `form-field-move-up` | FR-049 (non-drag alternative) |
| Field card: move down | `form-field-move-down` | FR-049 |
| Field card: remove | `form-field-remove` | FR-009 |
| Add-field control, offering all seven types | `form-add-field` | FR-003 |
| Empty canvas state ("Feld hinzufügen, um zu beginnen") | `form-field-canvas-empty` | FR-011 |
| The published link, shown once ≥1 field exists | `form-builder-link` | FR-012 |
| "Formular hat noch kein Feld — Link nicht erreichbar" | `form-builder-no-link` | FR-011 |
| Responses list | `form-responses` | FR-040 |
| One response row | `form-response-row` | FR-040 |
| Delete one response | `form-response-delete` | FR-042a |
| CSV export | `form-export-csv` | FR-034 |
| JSON export | `form-export-json` | FR-035 |
| Empty responses state | `form-responses-empty` | US3 |

**Remove-field confirmation** states that every value already collected for that field will be
destroyed (FR-009). Test id `form-field-remove-confirm`. It is skipped only when the field has
zero collected values.

**Delete-one-response confirmation** states that the form and its other responses are unaffected
(FR-042a). Test id `form-response-delete-confirm`.

**Drag reordering** (FR-007, research R-6): `draggable` on `form-field-card`, updating
`FormField.DisplayOrder` via `PUT /admin/forms/{formId}/fields/order` on drop. The move-up/move-down
buttons call the same endpoint with a locally computed new order, so both paths produce
identical, testable server state — an end-to-end test may use either without the assertions
differing.

## 5. The participant surface — `FormFillView.vue` (`/f/:formToken`)

One continuous page, fields in builder order (FR-013).

| Element | `data-testid` | Requirement |
|---|---|---|
| Form title | `form-fill-title` | FR-013 |
| One field, by type (see §5a) | `form-fill-field` | FR-013, FR-014 |
| Required marker (text, not colour) | `form-fill-required-marker` | FR-014, FR-057-equivalent (this feature's FR-050 is the accessibility rule this marker follows) |
| Field-level error, shown inline | `form-fill-field-error` | FR-016, FR-017 |
| Submit | `form-fill-submit` | FR-016 to FR-018 |
| Confirmation after a successful submit | `form-fill-confirmation` | FR-021 (US2 scenario 7) |
| Unknown/malformed/zero-field-form notice | `form-fill-unavailable` | FR-021 |
| Maintenance notice | `form-fill-maintenance` | FR-045 |

### 5a. Control per field type

| Field type | Control | Unanswered state |
|---|---|---|
| Text | `v-text-field` (or `v-textarea` past a length threshold the component decides) with a live character counter against `maxLength` | empty string |
| Integer | `v-text-field` with numeric input mode, digits and a leading `-` only | empty |
| Decimal | `v-text-field` with numeric input mode, digits, `.` and a leading `-` only | empty |
| Boolean | `v-radio-group` (Ja / Nein), **no option pre-selected** | no option selected (research R-13 — never a `v-checkbox`) |
| Email | `v-text-field type="email"` | empty |
| Phone | `v-text-field type="tel"` | empty |
| PostalCode | `v-text-field` with numeric input mode, 5-digit affordance | empty |

**Submission is blocked client-side** (FR-016) while any required field is empty/unselected or any
filled field fails its type's check (data-model.md §6); the first invalid field receives focus.
This client check and the server's (contracts/openapi.yaml, `400` on
`POST /f/{formToken}/responses`) share one rule table (research R-7) — an end-to-end test that
bypasses the client (a direct API call) must see the exact same field rejected as the UI would
have blocked.

**Must not exist on this surface**: any navigation into `/admin` or `/e`; any indication of who
built the form; any way to edit a response after submitting (FR-021's confirmation is terminal).

## 6. Text, accessibility and layout

- All new strings come from `de.json` (FR-048). New top-level key: `form`. New `nav.forms`.
- Every control keyboard-operable, text-labelled, with visible focus, including the drag handle's
  non-drag alternative (FR-049).
- The builder and the fill-in surface are usable at 375 px (FR-050).
- Required-vs-optional is conveyed in text (the marker's accessible name), never by colour alone.

## 7. Stores

Two new Pinia stores, mirroring the `wishLists.ts` / (public equivalent) split 009 already uses:

- `forms.ts` — the operator's store for `FormsView.vue` and `FormBuilderView.vue`: list, current
  form detail, field mutations, response list, response deletion, export URL builders
  (`formExportCsvUrl(formId)`, `formExportJsonUrl(formId)` — navigated to directly, not fetched,
  matching the existing download pattern for `exportUrl`/`backupUrl`).
- `formFill.ts` — the participant store for `FormFillView.vue`: the token from the route, the
  fetched definition, the in-progress answers, submission state. Kept separate from `forms.ts` for
  the same reason 009 keeps `creator.ts` separate from `creators.ts` — sharing a store would be
  sharing a URL prefix, and the first mistake would be a participant request going to an admin
  endpoint.

No store here needs an owner concept, an isolation boundary, or a second request budget beyond the
one `formFill.ts` already inherits from the existing submission rate limit (research R-9).
