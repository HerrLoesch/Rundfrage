# Quickstart: Date Poll

**Feature**: 002-date-poll | **Date**: 2026-09-02

What this feature must make true, described as the developer and the operator will experience it.
It extends the 001 quickstart rather than replacing it.

---

## Configure the operator account

The admin area needs one account, and the deployed configuration must never contain the password
itself (FR-045a). Generate a hash and put *that* in the environment:

```bash
docker compose run --rm app dotnet Rundfrage.Api.dll --hash-password
# prompts on stderr, prints on stdout:
#   pbkdf2-sha256:600000:<salt>:<hash>
```

The prompt goes to stderr so the hash can be redirected straight into a file. The separator is
`:` and not the conventional `$` of the PHC format, because Docker Compose reads `$name` in a
`.env` value as a variable reference — with `$` the hash reaches the container mangled and no
password ever verifies. Both were found by running it.

```bash
# .env  (git-ignored)
ADMIN_USER=hendrik
ADMIN_PASSWORD_HASH=pbkdf2-sha256:600000:...:...
```

There is no registration and no password-change screen. Changing the password means changing the
configuration and restarting — the deliberate consequence of having exactly one account
(FR-045b).

> Unlike feature 001, this **cannot** run on defaults. A poll-creating admin area with a
> guessable built-in password would be worse than no protection at all, so the application
> refuses to start without both variables set.

---

## Create a poll

1. Open <http://localhost:8080> — the root leads to the admin area — and sign in.
   The walking-skeleton diagnostic from feature 001 now lives at `/status`.
2. Give the poll a title, optionally a short message, and pick the candidate days.
3. Save. The participant link appears — copy it and share it.

The list shows every poll with its link and its **retention deadline**: the last candidate day
plus 30 days. Nothing disappears without having said when it would (FR-039a).

| Limit | Value |
|---|---|
| Title | 300 characters |
| Message | 2000 characters |
| Candidate days | 100 |
| Responses per poll | 1000 |

Past days are allowed — a poll can cover a period already under way (FR-014).

---

## Answer without an account

Open the participant link. The title, the message, the days, the current grid and the answer form
are all there on the first load. **No account, no sign-in, no email address, no confirmation
step.** Enter a name, mark the days, submit.

- Leaving a day unmarked is a valid answer: it means *keine Angabe* and nothing is stored for it.
- The page states, **before** the name field, that the name and answers are visible to everyone
  holding the link (FR-036a). Nobody should discover that after submitting.
- After submitting you get a **personal link**. It is the only way back to your answer — there is
  no account to look it up with. Losing it means answering again as a second response, which the
  system cannot and deliberately does not prevent.

---

## What the results show

```text
              03.10.  04.10.  05.10.
  Anna          Ja      Nein    Vielleicht
  Bernd         Ja      Ja      —
  Christa       —       Nein    Ja
  ------------------------------------------
  Ja             2       1       1
  Vielleicht     0       0       1
  Nein           0       2       0
```

The three totals **do not have to add up to the number of responses** — *keine Angabe* is not
counted (FR-033). How many people answered at all is read from the number of rows, which is why
every response is always shown (FR-033a).

The dash and *Nein* are distinguishable without colour (FR-053), and every state carries a
character or word rather than relying on a colour alone.

---

## Verify the things that are easy to get wrong

```bash
# The four not-found causes must be indistinguishable (SC-012)
curl -s http://localhost:8080/api/v1/polls/aaaaaaaaaaaaaaaaaaaaaa   # unknown
curl -s http://localhost:8080/api/v1/polls/short                    # malformed
# ... and an expired and a deleted token. All four: byte-identical.

# The admin area must disclose nothing without a session (FR-002, FR-048)
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:8080/api/v1/admin/polls   # 401

# Time zone: this is the one that broke in the container, not in tests (research.md R-6)
docker compose exec app sh -c 'ls /usr/share/zoneinfo/Europe/Berlin'
```

---

## Run the tests

```bash
dotnet test backend/Rundfrage.slnx    # xUnit: unit + integration (needs Docker)
cd frontend && npm run test:unit      # Vitest
docker compose up -d --build && cd e2e && npx playwright test
```

Three end-to-end specs matter most here and are worth running deliberately:

| Spec | Proves |
|---|---|
| `zero-signup.spec.ts` | A complete response from a bare link, with no account, no session and no stored credentials (FR-047, Principle I) |
| `admin-access.spec.ts` | Every admin route refuses and discloses nothing without a session (FR-048) |
| `date-poll-journey.spec.ts` | create → answer → revise → delete, end to end |

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Application exits at startup with a configuration error | `ADMIN_USER` or `ADMIN_PASSWORD_HASH` is unset | Set both. There is no default, by design. |
| `TimeZoneNotFoundException` on creating the first poll | The runtime image lacks tzdata | Ensure `apk add --no-cache tzdata` is in the runtime stage (research.md R-6) |
| Participant link returns not-found immediately after creation | The last candidate day is more than 30 days in the past, so the poll was born expired | Pick a later day; expiry is checked on access (FR-039b) |
| A submission returns 429 | More than 10 answers from one source within an hour (FR-027a) | Wait; the response says how long. Several people behind one connection share the limit. |
| A submission returns 409 | The poll holds 1000 responses (FR-015a) | Delete responses, or create a new poll |
| Signing in returns 429 with a correct password | Locked out after 5 failures (FR-005) | Wait 15 minutes. There is deliberately no reset path. |
