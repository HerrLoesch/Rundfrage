# Phase 0 Research: Date Poll

**Feature**: 002-date-poll | **Date**: 2026-09-02

Twelve decisions had to be settled before the design could be written. The theme running through
most of them is Principle I: because participants must never be identified, every capability a
participant has must be carried by a token in a URL, and every mechanism that would normally rest
on an identity has to be rebuilt on something else.

One item (R-6) is not a judgement call but a verified defect in the current container build.

---

## R-1: How the single operator authenticates

**Decision**: ASP.NET Core cookie authentication. One `HttpOnly`, `Secure`, `SameSite=Strict`
cookie, sliding expiration of 8 hours (FR-006). No token is ever readable by JavaScript.

**Rationale**: The application is a single origin serving both the SPA and the API (inherited
from 001), which is exactly the case cookie authentication is built for. Because the cookie is
`HttpOnly`, a cross-site scripting flaw cannot exfiltrate the session — which a token held in
`localStorage` would permit. Sliding expiration maps directly onto FR-006's "8 hours without
activity". Nothing new is added to the dependency list.

**Alternatives considered**:
- *JWT in `localStorage`* — rejected. It buys statelessness the feature has no use for (one
  account, one instance) and pays for it with an XSS-readable credential.
- *HTTP Basic on every request* — rejected. The browser's own credential caching makes FR-007
  (deliberately ending a session) unreliable, and FR-005's lockout has nowhere to live.

---

## R-2: Hashing the operator password without a new dependency

**Decision**: PBKDF2-HMAC-SHA256 from the BCL (`Rfc2898DeriveBytes`), 600,000 iterations, a
128-bit random salt per hash, encoded as one self-describing string
(`pbkdf2-sha256$<iterations>$<salt>$<hash>`). The API exposes a `--hash-password` switch that
prints such a string; the operator puts the result in `ADMIN_PASSWORD_HASH`.

**Rationale**: FR-045a requires the deployed configuration to contain something from which the
password cannot be recovered, so the application only ever *verifies*. Something has to produce
the hash, and a switch on the application itself keeps that in one place instead of a README
incantation the operator has to get right. PBKDF2 is in the BCL, so Principle III's "justify
every dependency" question does not even arise. The iteration count follows current OWASP
guidance for PBKDF2-HMAC-SHA256. Encoding the parameters into the string means the count can be
raised later without invalidating existing hashes.

**Alternatives considered**:
- *`PasswordHasher<T>` from ASP.NET Core Identity* — rejected. It means taking a dependency on
  the Identity stack to use a single class, for a system with exactly one account and no user
  store.
- *Argon2* — rejected. Stronger, but every .NET implementation is a third-party package, and
  several carry native dependencies that would complicate the Alpine image. PBKDF2 at 600,000
  iterations is sufficient for a single credential that is additionally protected by FR-005's
  lockout.

---

## R-3: Minting capability tokens

**Decision**: 16 bytes from `RandomNumberGenerator`, encoded base64url into 22 characters. Two
independent tokens exist: one per poll (grants sight and the right to answer) and one per
response (grants revision of that response only). Stored as-is in a unique indexed column.

**Rationale**: 16 bytes is 2^128, comfortably above SC-006's 2^120 floor. A cryptographic
generator satisfies FR-017's prohibition on deriving the token from the title, the days, or a
counter. Base64url keeps the link copy-pasteable and free of characters that need escaping.

**Tradeoff accepted and recorded**: the token is stored in plaintext rather than hashed, so
read access to the database is read access to every link. Hashing would prevent the direct
indexed lookup that every request performs. This is judged acceptable because database read
access already exposes every poll, every name and every answer — the token protects the link, not
the data behind it. It also means FR-043's ban on tokens in logs is the only place a token can
leak, which is why it is asserted by a test rather than trusted.

---

## R-4: One neutral not-found, not four

**Decision**: A single `NeutralNotFound` helper produces the identical status, body and headers
for all four causes in FR-027 and FR-040. Malformed tokens are **not** rejected early: they run
the same lookup path as well-formed ones, so a malformed token and an unknown one also cost
roughly the same time.

**Rationale**: SC-012 requires the four cases to be byte-identical, which four separate handlers
would break the first time one of them gained an extra detail. Routing all four through one
helper makes the requirement structural rather than a matter of discipline. Skipping the lookup
for malformed input would be the obvious optimisation and is deliberately not taken: a
measurable timing difference would let someone distinguish "never existed" from "wrong shape",
which is the distinction SC-012 exists to deny.

**Alternatives considered**:
- *Validate the token shape first and return early* — rejected, as above.
- *Return 410 Gone for expired polls* — rejected. It is more honest HTTP and precisely the
  disclosure SC-012 forbids.

---

## R-5: Rate-limiting submissions without storing who submitted

**Decision**: The rate limiter built into ASP.NET Core, a fixed window partitioned by remote
address, 10 permits per hour, applied only to the submit endpoint. Partition keys live in memory
for the length of the window and are never written anywhere.

**Rationale**: FR-027a needs the limit; FR-027b and FR-042 forbid persisting the request source.
The built-in limiter's partitions are in-process state, which matches that constraint exactly —
the constraint is satisfied by construction rather than by remembering not to save something. No
new dependency: the middleware ships with the framework.

**Consequence to accept**: restarting the application clears the windows. For a self-hosted tool
with one instance this is not worth solving; persisting the counters would mean persisting the
request source, which FR-027b prohibits outright. The limit is an abuse speed bump, not a
security boundary.

---

## R-6: Europe/Berlin is unavailable in the current runtime image

**Decision**: Add `RUN apk add --no-cache tzdata` to the runtime stage of `docker/Dockerfile`.

**This is a verified defect, not a precaution.** Measured on 2026-09-02:

```text
mcr.microsoft.com/dotnet/aspnet:10.0-alpine
  /usr/share/zoneinfo/Europe/Berlin            -> missing
  TimeZoneInfo.FindSystemTimeZoneById(...)     -> TimeZoneNotFoundException
                                                  "The time zone ID 'Europe/Berlin' was not
                                                   found on the local computer."
same image + apk add --no-cache tzdata
  TimeZoneInfo.FindSystemTimeZoneById(...)     -> OK: (UTC+01:00) Europe/Berlin
image size cost                                -> ~1 MB (184 MB -> 185 MB)
```

**Rationale**: FR-011a resolves every day boundary against Europe/Berlin and FR-011b requires
summer time to be handled. Without tzdata the application throws on the first date calculation —
that is, on the first poll created. The SDK image resolves the zone happily, so this defect
cannot be caught by any test that runs outside the runtime image; only the end-to-end suite
against Compose would have found it, and only at runtime.

**Alternatives considered**:
- *A fixed UTC+1/UTC+2 offset* — rejected. It would need hand-maintained summer-time rules, which
  FR-011b exists to avoid.
- *Switch the runtime image to Debian-based* — rejected. It carries tzdata by default but costs
  well over 100 MB against roughly 1 MB for the targeted fix.
- *`DOTNET_SYSTEM_TIMEZONE_...` / invariant mode* — rejected: does not supply zone data, it only
  changes how its absence is handled.

---

## R-7: A grid of 100,000 cells

**Decision**: Per-day totals are computed in the database with a grouped query and returned
separately from the rows. Response rows are paged, 50 per page. The 100 day columns scroll
horizontally within their own container.

**Rationale**: FR-036c and SC-016 require 1000 responses across 100 candidate days to become
usable within 5 seconds. Rendering 100,000 cells is not viable, so a reduction is mandatory
rather than anticipated — which is what keeps this inside Principle III, and why it is
nonetheless recorded in Complexity Tracking. Computing the totals with a grouped query rather
than from loaded rows means the summary — the part a reader actually acts on — never depends on
how many rows were fetched.

**Alternatives considered**:
- *Virtualised scrolling over the full grid* — rejected. It preserves the illusion of one
  continuous table at the cost of a new dependency and markedly more complexity than paging, for
  a view whose primary content is the totals row.
- *Aggregate-only for large polls* — rejected. FR-032 and FR-036 require every response to be
  visible with its name; silently dropping rows above a threshold would violate both.

---

## R-8: Storing "no answer" as nothing at all

**Decision**: A `DayAnswer` row exists only for a day a participant actually answered. Absence of
a row *is* the *no answer* state. The stored enum has three values, not four.

**Rationale**: This is the design that makes FR-033 true by construction — a grouped count over
existing rows cannot count what was never written, so the totals naturally cover only answered
states without any filtering to remember. It also stores dramatically less: a poll at the FR-015
limits where participants answer half the days holds roughly 50,000 rows instead of 100,000, and
Principle IV asks for exactly that. FR-024's "recorded as no answer" is satisfied: absence is the
record, and it is unambiguous because a response's set of candidate days is fixed at creation.

**Alternatives considered**:
- *A four-valued enum with one row per response per day* — rejected. It doubles the row count at
  the limits, writes a row to mean "nothing happened", and would require every totals query to
  filter the fourth value out — a filter that, once forgotten, silently breaks FR-033.

---

## R-9: Enforcing the 1000-response cap under concurrent submission

**Decision**: A submission takes a row lock on its poll, counts existing responses, and inserts
within one transaction. Submissions to the same poll therefore serialise; submissions to
different polls do not.

**Rationale**: FR-015a says the 1001st submission is *refused*, and the edge case says the answer
is never accepted and then dropped. A count outside a transaction is a classic race: two
concurrent submissions both read 999 and both insert. Locking the poll row is the smallest
mechanism that makes the cap exact. At this scale — a poll receiving simultaneous answers from a
handful of people — the serialisation costs nothing measurable.

**Alternatives considered**:
- *Count without a lock and accept slight overshoot* — rejected. It would make FR-015a
  approximately true, and the specification does not say approximately.
- *A database check constraint* — rejected: expressing "at most 1000 children" as a constraint
  requires a trigger or a maintained counter column, both more machinery than the lock.

---

## R-10: Cross-site request forgery, given cookie authentication

**Decision**: Two layers, no anti-forgery token machinery. The session cookie is
`SameSite=Strict`, and every state-changing admin endpoint accepts only
`Content-Type: application/json`.

**Rationale**: `SameSite=Strict` means the browser does not attach the cookie to any request
originating from another site, which removes the classic forged-form attack outright. The JSON
content type is a second, independent barrier: a cross-site HTML form cannot send it, and any
scripted request that could is already subject to the same-origin policy. Adding a synchroniser
token on top would be a third mechanism guarding a door two mechanisms already hold shut — which
is the kind of accumulation Principle III asks to justify, and here it cannot be.

---

## R-11: The tri-state control is a radio group

**Decision**: Each candidate day is a `fieldset` containing three native radio inputs — *Ja*,
*Vielleicht*, *Nein* — with the day as the legend. No custom widget.

**Rationale**: This satisfies all four accessibility requirements without writing accessibility
code. Native radios are keyboard-operable by default including arrow-key navigation (FR-050),
carry real labels (FR-051), show the platform focus ring (FR-052), and convey their state through
the control itself rather than through colour (FR-053). A custom three-button widget would have
to re-implement every one of those, and would be the most likely place for the colour-only trap
FR-053 exists to prevent. Leaving all three unselected is the *no answer* state, which needs no
representation at all (R-8).

**Alternatives considered**:
- *Three toggle buttons with ARIA roles* — rejected. Visually neater, and it means hand-writing
  roles, keyboard handling and focus management that the platform already provides correctly.

---

## R-12: Where the sign-in lockout lives

**Decision**: In memory, in a small `SignInThrottle` holding the consecutive failure count and
the lockout expiry for the single account.

**Rationale**: There is one account and one instance, so there is nothing to share. A database
table for two integers would be storage the feature does not need. FR-005a's requirements — the
lockout expires on its own after 15 minutes and resets on success — are simple in-memory state.

**Consequence to accept**: a restart clears the lockout. This is not a meaningful weakening: an
attacker who can restart the process has already lost the operator the machine, and legitimate
restarts are rare. It is recorded here so the choice is visible rather than discovered.

---

## Summary of changes to feature 001

This feature modifies two things the scaffold established:

1. **`docker/Dockerfile`** gains `apk add --no-cache tzdata` in the runtime stage (R-6). Without
   it the feature throws on its first date calculation.
2. **`RundfrageDbContext`** gains its first entities. The deliberately empty initial migration
   from 001 (research.md R-1 of that feature) now gets the successor it was built to receive, and
   001's FR-013a test — schema creation against an empty database — starts asserting something
   substantial.
