# Quickstart: Wunschliste (Wish List)

**Feature**: 008-wishlist

## What this feature is

The second thing Rundfrage can ask a group. A date poll asks *when*; a wish list asks *who brings
what*. The operator writes items with quantities, shares one link, and whoever holds it writes their
name against what they will bring. An item wanted three times holds exactly three names.

Read in this order: [spec.md](./spec.md) for what and why (its Clarifications section carries the
five decisions and their accepted consequences), [research.md](./research.md) for the fourteen
design decisions and the rejected alternatives, [data-model.md](./data-model.md) for the three new
tables and everything derived rather than stored,
[contracts/ui-contract.md](./contracts/ui-contract.md) for elements, states and test ids,
[contracts/openapi.yaml](./contracts/openapi.yaml) for the routes.

## The four things to get right

**1. Capacity is a transaction, not a check.** Count and insert inside one *immediate* write
transaction — reuse the helper in `ResponseService.SubmitAsync`, do not write a second one
(research R-1). The test that proves it fires 100 simultaneous claims at one open place and asserts
exactly one lands (SC-002). Write that test before the code.

**2. "Geschlossen" is the clock, not a column.** No status field anywhere. `IsClosed` is
`clock.Now > clock.EndOfDayUtc(list.TargetDate)`, evaluated per request (research R-3, FR-028c).
Moving the target date forward reopens the list with nothing to fix up. Test the boundary across a
summer-time transition — `BerlinClockTests` already shows how.

**3. Nothing deletes a wish list except the operator.** `RetentionService` stays poll-only. There is
an integration test whose whole job is to assert the hourly sweep leaves wish lists alone, because
"tidying up" by generalising that sweep is the plausible future mistake (FR-037, research R-11).

**4. One projection feeds both readers.** The wish-list area's list and the dashboard's overview
read `GET /api/v1/admin/wish-lists`. Do not write a second projection for the dashboard — FR-049's
agreement is meant to be structural (research R-5).

## The one thing that is not additive

`RestoreService`'s preview counts polls and responses. Once wish lists exist, a restore destroys
them too, and the confirmation the operator reads would understate the loss. The preview gains wish
lists and claims (research R-10, `openapi.yaml` → `RestorePreviewAddition`). This is the only change
this feature makes to existing behaviour, and it is a correction, not an extension.

## Running it

```bash
# Backend
cd backend && dotnet run --project src/Rundfrage.Api

# Frontend
cd frontend && npm run dev

# Checks that must be green (constitution gate 2)
cd frontend && npm run test:unit && npm run typecheck && npm run build
cd backend  && dotnet test
cd e2e      && npm test
```

The build fails on warnings and type errors, so `typecheck` is not optional.

The end-to-end suite needs two things that are easy to miss, and it fails unhelpfully without
either:

```bash
# The operator credentials of the running instance, and a raised submission budget. The suite
# sends far more than ten submissions per hour from one machine, so the default rate limit of
# FR-023 would refuse it halfway through - which is what it did.
SUBMISSION_LIMIT_PER_HOUR=1000 docker compose up -d --build
set -a && . ./.env && set +a          # E2E_ADMIN_USER / E2E_ADMIN_PASSWORD
cd e2e && npx playwright test
```

Raise the limit on the *container*, not in the shell running Playwright: the limiter lives in the
application, and the suite's own environment cannot reach it.

## The migration

One migration adds `WishLists`, `WishItems`, `WishClaims`. No existing table changes.

```bash
cd backend
dotnet ef migrations add WishLists --project src/Rundfrage.Api
dotnet test tests/Rundfrage.Api.IntegrationTests --filter "FullyQualifiedName~SchemaCreation"
```

`SchemaCreationTests` is what proves a fresh installation gets the tables; `DurabilityTests` and
`BackupTests` need no change, because the backup copies the file.

## Trying it by hand

```bash
# 1. Sign in (the e2e helpers in e2e/support/admin.ts do this for you)
# 2. Create a list
curl -X POST localhost:5000/api/v1/admin/wish-lists -b cookies -H 'Content-Type: application/json' \
  -d '{"title":"Sommerfest","targetDate":"2026-07-18","description":"Bitte eintragen",
       "items":[{"name":"Kuchen","wantedCount":2},{"name":"Grill"}]}'
# -> 201 with listToken; "Grill" has wantedCount 1 without anyone saying so (FR-006)

# 3. Claim, with no session at all (Principle I)
curl -X POST localhost:5000/api/v1/wish-lists/$LIST_TOKEN \
  -H 'Content-Type: application/json' -d '{"displayName":"Anna","itemIds":["<kuchen-id>"]}'
# -> 201 with claimToken; that is the personal link /z/<claimToken>

# 4. Fill it up, then watch the refusal name the state
#    -> 400 {"code":"item_full"}

# 5. Withdraw
curl -X DELETE localhost:5000/api/v1/claims/$CLAIM_TOKEN/$CLAIM_ID
# -> 204, and the place is open again
```

## Scale

FR-054 documents 200 lists holding up to 200,000 claims for SC-009's two seconds. That is two orders
of magnitude below what feature 007 measured for the dashboard (0.66 s at 2.5 million rows), so no
measurement spike gates this design — but the scale test is still written, so the claim is asserted
rather than assumed (research R-12). Per-list figures come from grouped aggregates; a query per list
would be 200 round trips inside one request and is the failure this test exists to catch.

## Things deliberately not built

Export/import per list, notifications or reminders, participant-proposed items, notes or quantities
per person, a second operator account, any automatic expiry. Each is in the spec's *Out of Scope*
with its reason; none is a gap waiting to be filled.
