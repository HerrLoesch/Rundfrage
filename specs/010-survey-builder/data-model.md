# Data Model: Individuelle Formulare (Custom Form Builder)

**Feature**: 010-survey-builder | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

Four new tables, no changes to any existing table, one migration. Unlike feature 009, this feature
adds **no** owner column and touches `OwnerScope` in no way (research R-3): every table here is
reached only through the operator's own `admin` session, exactly as `WishList` was before feature
009 gave it an owner.

## 1. `Form`

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `Title` | `string`, max 200 | Required (FR-001) |
| `FormToken` | `string` | `CapabilityToken.Mint()`, same token space as every other capability token (FR-012 → 002 FR-016/FR-017 pattern). Unique index. |
| `CreatedAt` | `DateTime` | |

No `CreatorId`. No expiry column — a form is deleted explicitly (FR-043) or it persists.

**Relationships**: one `Form` has many `FormField` (cascade delete) and many `FormResponse`
(cascade delete).

## 2. `FormField`

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `FormId` | `Guid` | Required FK → `Form` |
| `Type` | `FieldType` (string-backed enum) | Fixed at creation, never changed (FR-008; research R-2) |
| `Label` | `string`, max 200 | Required (FR-004) |
| `Required` | `bool` | Defaults `false` on add (FR-005) |
| `MaxLength` | `int?` | Required (non-null) when `Type == Text`; otherwise always `null` (FR-006). Bounded `1..5000` — a system ceiling, not a spec-numbered requirement, chosen so a single field cannot become an unbounded blob while comfortably exceeding any legitimate open-text answer. |
| `MinLength` | `int?` | Only meaningful when `Type == Text`; when set, `1 <= MinLength <= MaxLength`. `null` means no minimum. |
| `DisplayOrder` | `int` | Dense, `0..N-1` within one form, renumbered on every add/remove/reorder (research R-4) |

`FieldType` enum: `Text`, `Integer`, `Decimal`, `Boolean`, `Email`, `Phone`, `PostalCode` — exactly
the seven of FR-003.

**Index**: `(FormId, DisplayOrder)`, supporting both the ordered field list and the renumbering
transaction.

**Relationships**: one `FormField` has many `FormFieldValue` (cascade delete — deleting a field
deletes every collected value for it, FR-009).

## 3. `FormResponse`

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `FormId` | `Guid` | Required FK → `Form` |
| `SubmittedAt` | `DateTime` | |

No participant identity of any kind (FR-019, Principle IV): no IP address, no user agent, no
session, no name.

**Relationships**: one `FormResponse` has many `FormFieldValue` (cascade delete — deleting a
response deletes only its own values, FR-042a, and nothing else's).

## 4. `FormFieldValue`

| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `ResponseId` | `Guid` | Required FK → `FormResponse` |
| `FieldId` | `Guid` | Required FK → `FormField` |
| `Value` | `string` | Canonical, culture-invariant text form of the answer (see §5) |

**Index**: unique `(ResponseId, FieldId)` — one value per field per response.

A row's **absence** for a given `(ResponseId, FieldId)` pair means "not answered." This is the
whole mechanism behind FR-015's boolean unanswered state (research R-13) and FR-038's missing
value on export for a field added after a response existed (research R-14) — neither needs a
special case; both are the plain meaning of a missing row.

## 5. Canonical value encoding (how `FormFieldValue.Value` is written and read)

| Field type | Stored as | Example |
|---|---|---|
| `Text` | the string itself, unmodified | `Anna Beispiel` |
| `Integer` | invariant-culture integer text | `42`, `-3` |
| `Decimal` | invariant-culture decimal text, `.` as separator | `3.14` |
| `Boolean` | exactly `yes` or `no` (FR-026) | `yes` |
| `Email` | the string itself, unmodified | `anna@example.com` |
| `Phone` | the string as entered (not normalised) | `+49 30 1234567` |
| `PostalCode` | the string itself (leading zeros preserved) | `01067` |

Type-specific parsing back into a typed value happens only at the two points that need a typed
value — validation (§6) and export (contracts/openapi.yaml, `export/json` and `export/csv`) —
never by changing what is stored.

## 6. Validation rules (FR-023 to FR-030), the single source client and server both implement

| Field type | Rule |
|---|---|
| `Text` | Length (in characters) `>= MinLength` (when set) and `<= MaxLength`. |
| `Integer` | Parses as a whole number, invariant culture, no fractional part, no thousands separator. |
| `Decimal` | Parses as a number, invariant culture, fractional part optional. |
| `Boolean` | Exactly `yes` or `no` (FR-026 — matching this codebase's existing wire vocabulary for answer-shaped booleans, `PollResponse`/`DayAnswer`'s own `yes`/`maybe`/`no`); absent is only acceptable when the field is optional. A JSON boolean submitted over the wire is accepted and mapped to `yes`/`no` at the boundary; a literal JSON string `"true"`/`"false"` is correctly refused as neither. |
| `Email` | Matches local-part `@` domain-with-a-dot shape: `^[^\s@]+@[^\s@]+\.[^\s@]+$`. |
| `Phone` | After stripping spaces, dashes and parentheses (and an optional leading `+`), at least 7 digit characters remain and no other character does. |
| `PostalCode` | Exactly five digits: `^\d{5}$` (German format — research R-2's home spec assumption). |

A **required** field additionally fails validation when no value was submitted for it at all
(FR-016, FR-017) — checked identically for every type by whether a value is present, per §4.

Research R-7 records where this table is implemented twice (TypeScript, C#) and how the two
implementations are kept from silently disagreeing.

## 7. Migration

One migration, `AddForms`, following the timestamp-prefixed naming of the three that exist
(`InitialSqlite`, `WishLists`, `AddCreators`): creates `Forms`, `FormFields`, `FormResponses`,
`FormFieldValues`, their indexes, and the explicit cascade configuration in
`RundfrageDbContext.OnModelCreating` for all three parent→child relationships in this feature.

Every relationship in this feature is **required** (no relationship is optional the way
`Creator → Poll`/`Creator → WishList` is), so EF Core's own default behaviour for a required
foreign key is already `Cascade` — unlike feature 009, there is no framework default silently
producing the wrong outcome here. The cascade is still configured explicitly in
`OnModelCreating`, matching this codebase's existing style of stating it rather than relying on an
unstated default, but it is a clarity choice here, not a correctness one.

No data migration: this feature adds tables, not columns, so there is nothing to backfill on an
existing installation (mirrors 009 FR-013's reasoning, one step simpler — 009 needed a nullable
column *and* an implicit "existing rows become the operator's" rule; this feature needs neither).

## 8. What deliberately has no entity

- **No `OwnerScope` entry.** Confirmed in research R-3.
- **No dashboard aggregation.** The spec's Key Entities and Functional Requirements make no
  mention of `Form` on `DashboardView.vue`; extending the dashboard's fixed installation-wide
  figures to a new content type is a decision this feature does not make, so no `FormSummary`
  reaches the dashboard projection.
- **No draft/published state column on `Form`.** FR-011 ("the link is not offered while the form
  has zero fields") is enforced by `GET /f/{formToken}` refusing a zero-field form exactly like an
  unknown token (contracts/openapi.yaml), not by a status the operator sets explicitly. A form
  with fields is reachable; a form without them is not, and that is a computed property, not a
  stored one.
