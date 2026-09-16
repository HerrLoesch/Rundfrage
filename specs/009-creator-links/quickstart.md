# Quickstart: Ersteller-Links

**Feature**: 009-creator-links | **Date**: 2026-09-13

For a developer picking this feature up. Read [research.md](./research.md) R-1, R-2 and R-6 first;
they are the three decisions the rest of the code assumes.

## Run it

```bash
export SPECIFY_FEATURE=009-creator-links      # every Spec Kit command needs this (constitution)

# Backend
dotnet run --project backend/src/Rundfrage.Api            # http://localhost:5000
# Frontend
cd frontend && npm run dev                                 # proxies /api to the backend
```

The operator account comes from `ADMIN_USER` and `ADMIN_PASSWORD_HASH`; there is no default and the
application refuses to start without them. Generate a hash with:

```bash
dotnet run --project backend/src/Rundfrage.Api -- --hash-password
```

## Walk the feature by hand

1. Sign in, open **Ersteller** in the navigation (fourth entry, before Einstellungen).
2. Create one called `Anna`. Copy the link — it looks like `http://localhost:5173/e/<22 chars>`.
3. Open it in a **private window**. No sign-in. It greets you as Anna and shows two empty lists.
4. Create a wish list through it. Open its participant link in a third window and claim something.
5. Back in the admin area: the wish list appears in **Wunschlisten**, owned by Anna.
6. **Revoke** Anna's link. The private window's next request 404s. The wish list is still there,
   still Anna's, and its participant link still works.
7. **Reissue** a link for Anna. The new link reaches exactly the same content.
8. **Delete** Anna. The confirmation names both counts. Afterwards the wish list is gone and its
   participant link 404s.

## Where things live

| Concern | Path |
|---|---|
| The new table | `backend/src/Rundfrage.Api/Data/Entities/Creator.cs` |
| Ownership columns | `Poll.cs`, `WishList.cs` (`CreatorId`, nullable) |
| Schema + cascade | `Data/RundfrageDbContext.cs` (`OnModelCreating`) |
| Creator service | `backend/src/Rundfrage.Api/Creators/CreatorService.cs` |
| **The access filter** | `backend/src/Rundfrage.Api/Creators/OwnerScope.cs` |
| Operator endpoints | `Endpoints/Admin/CreatorAdminEndpoints.cs` |
| Creator endpoints | `Endpoints/Creator/CreatorEndpoints.cs` |
| Rate-limit policy | `Http/RateLimiting.cs` (`CreatorWritePolicy`) |
| Admin area | `frontend/src/components/admin/CreatorsView.vue` |
| Creator surface | `frontend/src/components/creator/CreatorSurface.vue` |
| Stores | `frontend/src/stores/creators.ts`, `creator.ts` |
| Component tests | `frontend/tests/unit/CreatorSurface.spec.ts`, `CreatorsView.spec.ts` |
| End-to-end | `e2e/tests/creator-journey.spec.ts` |
| Strings | `frontend/src/locales/de.json` → `creator`, `nav.creators` |

## The five rules that are easy to break

1. **Never inject `RundfrageDbContext` into a creator handler.** Use `OwnerScope`. That is what
   makes FR-033 structural instead of a review comment (R-1).
2. **Never mount a creator route under `/api/v1/admin`.** The maintenance middleware exempts that
   prefix, and mounting there would let Ersteller write into a database about to be replaced by a
   restore (R-6, FR-050).
3. **Configure the cascade explicitly.** EF Core's default for an optional relationship is
   `ClientSetNull` — deleting an Ersteller would hand its content to the operator instead of
   destroying it, which FR-020c forbids (R-4).
4. **"Not yours" must answer exactly what "no such thing" answers.** Always `NeutralNotFound.Result()`;
   never 403, never a message (FR-034, SC-003).
5. **Do not give the creator surface a second address.** One route, disclosures in component state
   (FR-028d, R-9). The inconsistency with feature 007 is intentional.

## Tests

```bash
dotnet test backend/Rundfrage.slnx
cd frontend && npm run test:unit && npx playwright test
```

Test-first is mandatory (Principle II). The order that matters most here:

- `OwnerScopeTests` — the access filter itself, before any endpoint exists. This is the one that
  defines what the feature is for.
- `CreatorIsolationTests` — the same promise through HTTP, table-driven over every creator route.
- `CreatorRevocationTests` — revoke destroys nothing; delete destroys exactly the right things.
- `CreatorSurfaceRefusalTests` — no upload route, no admin control, no dashboard reachable with a
  creator token, including by direct request.
- `MigrationOnPopulatedDatabaseTests` — pre-existing polls and wish lists survive and belong to the
  operator (FR-013, SC-009).

Raise `CREATOR_WRITE_LIMIT_PER_HOUR` in the end-to-end environment, for the same reason
`SUBMISSION_LIMIT_PER_HOUR` is raised there: one machine legitimately exceeds the production number
during a full run.
