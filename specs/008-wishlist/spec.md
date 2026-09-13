# Feature Specification: Wunschliste (Wish List)

**Feature Branch**: `008-wishlist`
**Created**: 2026-09-13
**Status**: Draft
**Input**: User description: "Ich will jetzt ein neues Feature, neben der Terminfindung. Es handelt sich um eine Wunschliste. Dabei soll der Admin verschiedene Items angeben und wie viele er sich wünscht standarmäßig 1). Dann kann er einen Link erzeugen und Leute können Ihre Namen für Einträge auf der Liste einträgen um zu sagen was sie mitbringen wollen. Es der Admin mehr als eine Sache wünscht, dann sollen sich exakt so viele Leute eintragen können, wie der Admin sich wünscht. Der Admin soll die Wunschliste auch nach dem Erstellen der Lite noch editieren können. Eine Wunschliste hat ein Zieldatum, einen Titel und ein Beschreibungsfeld. Die Wunschliste soll nur manuell gelöscht werden, wenn der Admin es sagt. Im admin bereich soll sie auch noch Statusinfos wie (einträge und % der gewünschten Items mit Nutzereintragungen enthalten). Im allgemeinen Dashboard soll es eine Übersicht über alle vorgenommenen Wunschlisten geben."

## Summary

Rundfrage can ask a group *when* they have time. It cannot ask them *who brings what*. This feature
adds the second question as its own capability beside date polls: a **Wunschliste** — a titled list
with a target date and a description, holding items the operator wishes for, each with a number of
how many of it are wanted. The operator shares one link; whoever holds it writes their name against
an item to say they will bring it, without an account and without a step between the link and the
form.

The quantity is the mechanic that makes the list more than a shared note. An item wanted three times
holds exactly three names — not two, not four. A participant sees what is still open before they
choose, and an item that is full says so instead of accepting a fourth name that nobody will notice
is surplus.

Three properties distinguish this feature from date polls and are worth stating up front, because
each one deliberately departs from a pattern feature 002 established.

**A wish list is not deleted by time.** A poll erases itself thirty days after its last candidate
day. A wish list does not: it exists until the operator deletes it. The target date closes it for
new entries and marks it as closed, and nothing more — no item, no name and no list is removed by a
date passing. The constitution's Principle IV is satisfied by the
explicit deletion path rather than by an expiry, and the spec says so rather than letting the
difference look like an oversight.

**The list stays editable after it is shared.** The operator adds items, renames them, raises the
wanted count and changes the title, description and target date while people are already entering
themselves. The link does not change and the entries already made survive. The one edit that
destroys something — removing an item, or lowering a count below the names already on it — is the
one the spec constrains.

**It is a second feature area, not a second kind of poll.** Feature 007 built a navigation bar
"shaped to hold" further capabilities and refused to place a placeholder in it. This is the first
capability to take one of those places: a "Wunschlisten" entry between "Terminfindungen" and
"Einstellungen", with its own list, its own detail destination and its own share of the dashboard.

## Clarifications

### Session 2026-09-13

- Q: Who may withdraw an entry — anyone holding the participant link, or only the person who made
  it? → A: **Only the person who made it, through a personal link issued on submission**, mirroring
  the personal response link of feature 002 (FR-026, FR-028). The whole list is visible to every
  link holder, so the alternative would not have exposed anything new — but it would have let any
  link holder strike out somebody else's promise to bring something, and the participant surface
  gives no way to tell who did it. Accepted consequence: a participant who loses the link cannot
  release their own place and must ask the operator, who can delete any entry (FR-043a). The link
  is issued per submission and covers exactly the entries that submission made (FR-022b), so a
  participant claiming three items at once keeps one link, not three.
- Q: Does a passed target date stop a wish list from accepting new entries? → A: **Yes, and the
  list is marked "geschlossen" wherever it is shown** (FR-028a, FR-028b). The list stays stored and
  stays readable — it becomes the record of who brought what. Closing is derived from the target
  date and the clock, not stored as a state anybody sets: moving the target date to a future day
  reopens the list (FR-028c), which is also the operator's remedy when an occasion is postponed.
  Two consequences are accepted rather than hidden. First, the day boundary reaches the participant
  path, which Principle I asks to keep free of obstacles — tolerated because it removes a form
  rather than adding a step, and because a closed list still shows everything it showed before.
  Second, withdrawal closes with it (FR-028d): once no new entry can fill the freed place, removing
  a name would leave an unfillable hole in the record, so after closing only the operator can
  delete an entry.
- Q: Does the dashboard name each wish list, or stay within feature 007's aggregate-only rule? →
  A: **One row per wish list — title, target date, open or closed, entries, filled share — which
  amends 007 FR-034** (FR-048a, FR-048b). Aggregates answer "how much is there"; the request asks
  which lists exist and how they stand, and a sum cannot say which one nobody has claimed anything
  from. 007 FR-034 forbade poll titles on the dashboard as data minimisation, but the interest it
  protects is the participants': a list title is written by the operator, for the operator, and
  names nobody. The amendment is therefore narrow — wish-list titles are permitted, participant
  names remain forbidden on the dashboard (FR-050), and nothing changes for polls, whose titles stay
  off it. Accepted consequence: at the documented scale of FR-054 the overview is 200 rows, so it is
  bounded in height and scrolls within itself (FR-048c) rather than pushing the rest of the dashboard
  off the screen.
- Q: FR-010 allowed 100 items × 50 wanted each — 5000 places — while capping entries per list at
  1000. Which limit governs? → A: **The separate entry cap is dropped; the sum of the wanted counts
  per list is capped at 1000 instead** (FR-010, FR-010a). The wanted counts already bound how many
  entries a list can hold, so a second independent ceiling could only ever contradict FR-017 — a
  list could show 4000 open places that nothing would accept, and its filled share could not pass
  20 %. Capping the wished places moves the limit to where the operator meets it, when they create
  the list or raise a count, instead of surfacing it to a participant as a refusal they cannot act
  on. Accepted consequence: an operator who wants 100 items must keep their quantities to an
  average of 10, and is told so at the moment they exceed it.
- Q: Does the rate limit count entries or submissions, when one submission may claim several items?
  → A: **Submissions — at most 10 per hour per request source, whatever each one claims**
  (FR-023). FR-016 invites a participant to take several items at once, and counting names would
  have punished exactly that: somebody claiming five things for a family would exhaust the budget in
  two visits. The unit that matters for abuse is the request, and how much one request can carry is
  already bounded by the items' wanted counts (FR-017) and the list's capacity (FR-010). Withdrawing
  an entry counts as one action against the same budget (FR-023a), so the personal link cannot be
  used to bypass it by alternating claim and withdrawal.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask for things and let people claim them (Priority: P1)

The operator creates a wish list for an occasion: a title, a target date, a few words of
description, and the items — "Kuchen", wanted twice; "Getränke", wanted three times; "Grill",
wanted once. Each item defaults to one until the operator says otherwise. Creating the list produces
a link. Whoever opens that link sees the occasion, the date, every item, how many of each are still
open and which names are already written down, types their name against what they will bring, and is
done — keeping the personal link they are handed in case they have to back out. When "Kuchen" has two names on it, it is full and offers nobody a third.

**Why this priority**: This is the feature. On its own it delivers the whole value — a group can
divide up what needs bringing — and every later story only reports on or adjusts what this one
produces.

**Independent Test**: Create a list with one item wanted twice, open the resulting link in a fresh
browser with no session, enter two names, and confirm the item then refuses a third while a
different item still accepts one; then withdraw one of the two through the personal link issued for
it and confirm the place is claimable again.

**Acceptance Scenarios**:

1. **Given** the operator is signed in, **When** they create a wish list with a title, a target date
   and at least one item, **Then** the list is stored and a participant link is shown.
2. **Given** the item form, **When** the operator adds an item without stating a number, **Then**
   the item is wanted exactly once.
3. **Given** a valid participant link, **When** it is opened with no session and no account,
   **Then** the title, the description, the target date and every item are shown with the number
   wanted, the number still open and the names already entered, and the entry form is on that same
   page.
4. **Given** an item wanted three times with one name on it, **When** a participant enters their
   name, **Then** the entry is stored, the item shows two names and one open place, and no manual
   reload was needed to see it.
5. **Given** an item whose open places are all taken, **When** a participant looks at it, **Then**
   it is marked as full, states that it is complete, and offers no way to enter a name.
6. **Given** an item with exactly one open place, **When** two participants submit a name for it at
   the same moment, **Then** exactly one entry is stored, the other is told the item filled up, and
   the item never holds more names than are wanted.
7. **Given** a participant on the list, **When** they enter names against several items in one
   visit, **Then** all of them are recorded without leaving the page and without signing in.
8. **Given** a participant who submits without a name, **When** the entry is attempted, **Then** it
   is refused naming the missing name, and nothing is stored.
9. **Given** a participant who has entered a name, **When** they open the personal link they were
   given and withdraw the entry, **Then** the entry is gone, the place is open again for somebody
   else, and no other entry on the list is affected.
10. **Given** a personal link, **When** its holder attempts to reach another entry, another wish
    list or any admin function through it, **Then** it grants none of them.

---

### User Story 2 - Change the list after it is out (Priority: P2)

Plans change after the link is sent. The operator opens the list in the admin area and edits it:
corrects the title, moves the target date, adds "Servietten", renames "Getränke" to "Alkoholfreie
Getränke", raises "Kuchen" from two to four because more people are coming. The link keeps working
and the names already entered stay where they are. Lowering a count below the names already on an
item is refused and says how many are entered; removing an item asks first and says how many names
it will destroy. When the occasion is over, the operator deletes the list themselves — nothing else
ever does.

**Why this priority**: A list that freezes on creation is wrong within a day of being shared, and the
request names editing explicitly. It is second only because a list must exist before it can be
edited.

**Independent Test**: Create a list, enter two names against an item, then rename that item, raise
its count, and confirm both names are still there under the new name; then attempt to lower the
count below two and confirm the refusal names the two entries; then delete the whole list and
confirm the participant link behaves like an unknown link.

**Acceptance Scenarios**:

1. **Given** a shared wish list, **When** the operator changes its title, description or target
   date, **Then** the change is stored, the participant link is unchanged, and the next person to
   open the link sees the new values.
2. **Given** a shared wish list, **When** the operator adds an item, **Then** it appears for link
   holders with its full number of open places and the existing items are untouched.
3. **Given** an item carrying entries, **When** the operator renames it, **Then** the entries stay on
   it and appear under the new name.
4. **Given** an item wanted twice and carrying two entries, **When** the operator raises the wanted
   count to four, **Then** two places open up and both existing entries remain.
5. **Given** an item wanted four times and carrying three entries, **When** the operator lowers the
   wanted count to two, **Then** the change is refused and the refusal states that three names are
   already entered.
6. **Given** an item carrying entries, **When** the operator removes the item, **Then** an explicit
   confirmation states how many entries will be destroyed, and only after confirming are the item
   and its entries gone.
7. **Given** a wish list whose target date has passed, **When** its link is opened, **Then** the
   list, its items and all of its entries are still stored and still shown, the list is marked as
   closed, and no form for a new entry is offered.
8. **Given** a closed wish list, **When** the operator moves its target date to a future day,
   **Then** the list is open again with its items and entries unchanged.
9. **Given** a wish list, **When** the operator deletes it and confirms, **Then** the list, its items
   and all of its entries are removed, and its participant link is thereafter indistinguishable from
   a link that never existed.
10. **Given** a single entry the operator wants gone, **When** they delete that entry, **Then** only
   that entry is removed, its place is open again, and the list's figures follow.

---

### User Story 3 - Read the state without opening anything (Priority: P3)

The operator wants to know whether the list is coming together. The wish-list area shows, per list,
how many entries have been made, what share of the wished-for places is filled, and how many items
nobody has taken at all. The dashboard carries that answer one level higher: one row per wish
list in the installation — title, target date, open or closed, entries, filled share — with what
still needs chasing at the top, so the operator can see across all of them before opening a single
one.

**Why this priority**: It reports on what the first two stories produce and is not needed to run a
list. It is the part that turns several lists into something an operator can supervise.

**Independent Test**: Create two lists with known items, counts and entries, and confirm that the
entry counts, the filled-places share and the untaken-items count shown in the area and on the
dashboard match a hand count; then delete one entry and confirm every affected figure follows.

**Acceptance Scenarios**:

1. **Given** wish lists exist, **When** the operator opens the wish-list area, **Then** each list is
   shown with its title, its target date, its number of entries, its share of filled places and its
   participant link.
2. **Given** a list with six wished places and three entries, **When** its status is read, **Then**
   it states three entries out of six places and a filled share of 50 %.
3. **Given** a list in which two of five items carry no entry at all, **When** its status is read,
   **Then** it states that two items are still untaken.
4. **Given** a displayed figure, **When** it is compared with what the list's own detail view shows,
   **Then** the two agree.
5. **Given** the operator changes something a figure counts, **When** they return to the area or the
   dashboard, **Then** the figure reflects the change without a manual reload of the page.
6. **Given** no wish list has ever been created, **When** the area or the dashboard is shown,
   **Then** it says so in words and does not present zeros that could be mistaken for a failure.
7. **Given** the stored data cannot be read, **When** the area or the dashboard is shown, **Then**
   it says the data is unreachable and shows no figure at all.
8. **Given** a list whose every place is filled, **When** its status is read, **Then** it says the
   list is complete rather than only showing 100 %.
9. **Given** several wish lists, **When** the dashboard is shown, **Then** each of them appears as a
   row naming its title, its target date, whether it is open or closed, its entries and its filled
   share, with open lists first and the nearest target date at the top.
10. **Given** a row in the dashboard's overview, **When** the operator follows it, **Then** they
    reach that wish list in the wish-list area in one action, and the dashboard itself offers no way
    to create, edit or delete a list.
11. **Given** any wish list with entries, **When** the dashboard is read, **Then** no participant's
    display name appears anywhere on it.

---

### Edge Cases

- **Two people take the last place at once.** The item must end with exactly as many names as are
  wanted. One submission wins, the other is told the item filled up and is shown the current state,
  so the loser understands what happened rather than seeing a generic failure.
- **A participant's view is stale.** Someone loads the page, goes to make tea, and submits against an
  item that filled up meanwhile. The submission is refused on the current state of the list, never
  accepted on the state the browser still shows.
- **The operator removes an item while someone is entering a name for it.** The submission must fail
  with a message saying the item is no longer on the list, and must not recreate it.
- **A raise that would exceed the list's capacity.** Raising a wanted count, or adding an item, so
  that the wished places would pass 1000 is refused and names how many places are still available.
  Nothing about it reaches participants.
- **The wanted count is lowered.** Below the number of names already entered it is refused (US2, AS5).
  Lowering to exactly that number is allowed and leaves the item full with no open places.
- **One person claims many items at once.** A single submission carrying several names spends one
  of the ten allowed per hour, not one per name, so the path FR-016 invites is not the path the rate
  limit punishes.
- **A participant loses their personal link.** They cannot release their own place; only the
  operator can, by deleting the entry from the admin area (FR-022d, FR-043a). The participant page
  must say who to ask rather than offer a recovery that would require identifying the participant.
- **A personal link whose entry is already gone** — withdrawn twice, or deleted by the operator in
  the meantime — must say the entry is no longer there rather than fail, and must not offer to
  restore it.
- **The same name twice.** Two people called Anna may both bring a cake. Nothing may refuse a name
  because it is already on the item or elsewhere on the list; names are labels, not identities.
- **The target date has passed.** The list is not deleted and not hidden: it closes. Everything it
  showed is still shown, marked as closed, with no entry form and no withdrawal — the only way back
  to an open list is the operator moving the target date.
- **A submission arrives as the day turns.** Whether it is accepted is decided on the moment it
  arrives against the fixed zone, so a participant whose page was loaded before midnight is told the
  list has closed rather than silently having their entry dropped or accepted.
- **A list with no items.** Creation requires at least one item, and removing the last remaining item
  from an existing list leaves a list that says it holds nothing to claim yet rather than an empty
  page that reads as broken.
- **Storage is unreachable.** As in every other admin area, "there is nothing here" and "this cannot
  be read right now" must look different, and no figure may be shown as a zero when the truth is that
  it could not be read.
- **A link to a deleted list.** Indistinguishable from an unknown link — the operator's deletion must
  not be inferable from the response.
- **Maintenance mode is on.** The participant surface of a wish list must be refused exactly as the
  poll surface is, and no entry may be accepted while the installation is being restored.
- **A long list on a narrow screen.** A hundred items with names under them must stay readable and
  enterable on a phone; the open/full state must not depend on colour alone.
- **A very popular item.** An item wanted many times accumulates many names; the participant page
  must stay usable at the limits this spec sets, without the entry form being pushed off screen.

## Requirements *(mandatory)*

### Functional Requirements

#### The wish list and its items

- **FR-001**: A wish list MUST have a title. Creation without one MUST be refused, naming what is
  missing.
- **FR-002**: A wish list MUST have a target date (Zieldatum): one whole calendar day, resolved
  against the fixed zone Europe/Berlin as every other day boundary in the system is (002 FR-011a).
  Creation without one MUST be refused.
- **FR-002a**: A target date in the past MUST be permitted, so that a list can be created for an
  occasion already under way (mirrors 002 FR-014).
- **FR-003**: A wish list MAY have a description. Its absence MUST NOT prevent creation.
- **FR-004**: A wish list MUST have at least one item at creation. Creation without an item MUST be
  refused.
- **FR-005**: Every item MUST have a name and a wanted count (Wunschanzahl) stating how many of it
  are wished for.
- **FR-006**: When the operator states no number for an item, the wanted count MUST be 1.
- **FR-007**: The wanted count MUST be a whole number of at least 1. Zero, a negative number and a
  fraction MUST be refused, naming the item.
- **FR-008**: Item names MUST be distinct within one wish list. A duplicate MUST be refused and MUST
  name the item it collides with.
- **FR-009**: Items MUST be presented in the order the operator entered them, and MUST appear in that
  same order to the operator and to participants.
- **FR-010**: The following limits MUST be enforced on the server, not only in the form, and a
  violation MUST be refused naming the field and its limit:
  - title: 300 characters
  - description: 2000 characters
  - item name: 200 characters
  - display name of an entry: 100 characters
  - items per wish list: 100
  - wanted count per item: 50
  - wished places per wish list — the wanted counts of all its items added together: 1000
- **FR-010a**: The cap on wished places MUST be checked whenever the operator creates a list, adds
  an item or raises a wanted count, and the refusal MUST state the cap and how many places are still
  available. It MUST NOT be checked when a participant submits: the number of entries a list can
  hold follows from the wanted counts (FR-017), so no participant may ever be refused on the grounds
  of a list-wide entry limit.
- **FR-011**: Creating, editing and deleting a wish list MUST require an authenticated operator
  session and MUST be refused otherwise without revealing whether the named list exists
  (002 FR-001, FR-002).

#### The participant link and entering a name

- **FR-012**: On creation the system MUST generate a participant link containing an unguessable
  token, drawn from a space large enough that guessing a valid link is not a practical attack
  (002 FR-016, FR-017).
- **FR-013**: Opening a valid participant link MUST present, on one page: the title, the description,
  the target date, every item with its wanted count, how many places are still open, the names
  already entered, and the form for entering a name — the last of these only while the list is open
  (FR-028b).
- **FR-014**: Entering a name MUST NOT require an account, a sign-in, an email address, a
  confirmation or an installation of anything, and no step of any kind may be placed between the link
  and the entry form (Principle I; 002 FR-020, FR-021).
- **FR-015**: A participant MUST provide a display name for an entry. It is a label only and MUST NOT
  be treated as an identity, MUST NOT be verified, and MUST NOT be required to be unique.
- **FR-016**: A participant MUST be able to claim several items in one visit and submit them without
  navigating away from the page they landed on (002 FR-025).
- **FR-017**: An item MUST accept at most as many entries as its wanted count. An entry beyond that
  number MUST be refused, stating that the item is complete.
- **FR-017a**: When several entries for the last open place of one item arrive at the same time,
  exactly one MUST be stored. The others MUST be refused with the message of FR-017. Under no
  sequence of concurrent submissions may an item hold more entries than its wanted count.
- **FR-017b**: A submission MUST be judged against the state of the list at the moment it arrives,
  never against the state the participant's page was showing. A refusal MUST be accompanied by the
  current state of that item.
- **FR-018**: An item with no open places MUST be visibly marked as complete and MUST NOT offer a way
  to enter a name.
- **FR-019**: Everyone holding the participant link MUST see the names already entered against every
  item, so that a participant can see what is taken before choosing (mirrors 002 FR-036, and is the
  point of a bring-list).
- **FR-019a**: Before a participant enters a display name, the page MUST state plainly that the name
  will be visible to everyone holding the link (002 FR-036a).
- **FR-020**: The same display name MUST be accepted more than once, both on different items and on
  the same item. Nothing may refuse an entry on the grounds that the name is already present.
- **FR-021**: An entry MUST become visible on the page without a manual reload of the page for the
  participant who made it, together with the item's updated number of open places (002 FR-035).
- **FR-022**: On submission the system MUST issue a personal link containing its own unguessable
  token, through which the participant can view and withdraw what they entered, so that a place
  claimed by mistake or given up can be released without the operator (002 FR-026).
- **FR-022a**: Withdrawing an entry MUST remove it and MUST open its place again immediately, so
  that the item can be claimed by somebody else. The figures of FR-045 MUST follow.
- **FR-022b**: One submission MUST produce exactly one personal link, and that link MUST cover
  exactly the entries made in that submission — a participant claiming three items at once receives
  one link for the three, not three links. A later submission through the same participant link MUST
  produce its own personal link rather than extend an earlier one.
- **FR-022c**: A personal link MUST NOT grant access to any other entry, to any other wish list, or
  to any admin function (002 FR-029). Holding it MUST NOT reveal anything the participant link does
  not already show.
- **FR-022d**: Without the personal link, no participant-facing route MUST permit removing or
  altering an entry. The operator remains able to delete any entry from the admin area (FR-043a),
  which is the only recourse for a participant who has lost their link.
- **FR-022e**: After the wish list is deleted, every personal link issued for it MUST behave exactly
  as an unknown link does (FR-024; 002 FR-040).
- **FR-023**: Submissions MUST be rate-limited to at most 10 per hour per request source, counted as
  submissions regardless of how many items each one claims. A participant claiming several items in
  one submission (FR-016) MUST spend exactly one of the ten. The request source MAY be used for this
  purpose only transiently, in memory, and MUST NOT be stored, logged or associated with any entry
  (002 FR-027a, FR-027b). A refused submission MUST say that too many entries were sent and that it
  can be retried later (002 FR-027c).
- **FR-023a**: Withdrawing an entry through a personal link (FR-022) MUST count as one action
  against the same budget, so that alternating a claim and a withdrawal cannot circumvent FR-023.
- **FR-024**: An unknown, malformed or deleted wish-list link MUST produce one response that does not
  distinguish between those cases (002 FR-027).
- **FR-025**: While maintenance mode is on, the participant surface of a wish list MUST be refused
  with the notice the participant surface already uses, and no entry may be stored (feature 005).
- **FR-026**: The participant surface of a wish list MUST NOT show the admin shell, its navigation,
  or any link into the admin area (007 FR-009).

#### The target date

- **FR-027**: The target date MUST be shown to the operator and to participants wherever the list is
  presented.
- **FR-028**: The passing of the target date MUST NOT delete a wish list, any item or any entry, and
  MUST NOT make the participant link stop working.
- **FR-028a**: Once the target day has ended — 23:59:59 in the fixed zone of FR-002 — the wish list
  MUST stop accepting new entries. The target day itself is still open; closing takes effect only
  after it has passed.
- **FR-028b**: A closed wish list MUST be marked as closed ("geschlossen") wherever it is shown, on
  the participant surface and in the admin area alike. The participant surface MUST continue to show
  the title, the description, the target date, every item and every name entered, and MUST NOT offer
  a form for a new entry; it MUST say that the list is closed rather than leave the missing form
  unexplained.
- **FR-028c**: Being closed MUST be derived from the target date and the current moment, not stored
  as a state that anything sets or clears. Changing the target date to a day that has not yet ended
  MUST therefore reopen the list, with its items and entries unchanged, and no separate control to
  open or close a list may exist.
- **FR-028d**: While a list is closed, withdrawing an entry (FR-022) MUST also be refused, stating
  that the list is closed, because a place freed after closing can no longer be claimed by anybody.
  The operator MUST remain able to delete any entry (FR-043a).
- **FR-028e**: Whether a submission is refused as closed MUST be judged against the moment it
  arrives, not against the state the participant's page was showing (as FR-017b requires of a full
  item). A refusal MUST say the list has closed.

#### Editing a shared list

- **FR-029**: The operator MUST be able to change a wish list's title, description and target date at
  any time for as long as the list exists, and the change MUST be what link holders see from then on.
- **FR-030**: The operator MUST be able to add items to an existing wish list. A new item MUST start
  with its full wanted count open and MUST NOT affect existing items or entries.
- **FR-031**: The operator MUST be able to rename an item. Its entries MUST stay on it and MUST be
  shown under the new name.
- **FR-032**: The operator MUST be able to raise an item's wanted count. The raise MUST open the
  corresponding number of places and MUST leave existing entries untouched.
- **FR-033**: The operator MUST be able to lower an item's wanted count down to, but not below, the
  number of entries already on it. An attempt to lower it further MUST be refused and MUST state how
  many entries are already there.
- **FR-034**: The operator MUST be able to remove an item. Removal MUST require an explicit
  confirmation that states how many entries will be destroyed with it, and MUST destroy exactly those
  entries and nothing else.
- **FR-035**: No edit MUST change or invalidate the participant link. The token stays the same across
  every edit.
- **FR-036**: No edit other than FR-034 and the deletion of a single entry (FR-043) MUST remove an
  entry. An entry submitted while the operator was editing MUST NOT be lost by the edit being saved.

#### Deletion and retention

- **FR-037**: A wish list MUST be removed only when the operator explicitly deletes it. The system
  MUST NOT expire wish lists, MUST NOT derive a deletion date from the target date or the creation
  date, and MUST NOT include wish lists in any background erasure.
- **FR-038**: Deleting a wish list MUST require an explicit confirmation that names the list and
  states how many entries will be destroyed (002 FR-038).
- **FR-039**: Deletion MUST remove the wish list, its items and all of its entries. Afterwards the
  participant link MUST behave exactly as an unknown link does (FR-024; 002 FR-040).
- **FR-040**: The operator-initiated deletion of FR-037 is the wish list's retention outcome under
  Principle IV. It MUST therefore be reachable in one action from the wish-list area, and MUST erase
  the data rather than hide it.

#### The wish-list area in the admin shell

- **FR-041**: Wish lists MUST have their own admin area with its own address, reached from a
  navigation entry labelled "Wunschlisten", placed in the middle section of the navigation — after
  "Dashboard", before "Einstellungen" (007 FR-002a, FR-003, FR-004, FR-005).
- **FR-042**: The area MUST open on the list of wish lists. Creating a wish list MUST be an action
  that reveals its form when chosen rather than a form that is permanently open, and leaving the area
  MUST discard an unconfirmed entry rather than keep it waiting (007 FR-014h, FR-014j).
- **FR-042a**: When no wish list exists at all, the area MUST say so and offer creating one directly
  from that empty state (007 FR-014l).
- **FR-043**: A single wish list MUST be its own destination with its own address, reachable in one
  action from the list and reloadable and linkable like every other admin address (007 FR-005,
  FR-014a). It MUST carry the list's items, their wanted counts, their entries with the names, the
  editing actions of FR-029 to FR-034, and the deletion of a single entry.
- **FR-043a**: The operator MUST be able to delete one entry without affecting the rest of the list.
  The place MUST become open again and every figure of FR-045 MUST follow (002 FR-037a, FR-037b).
- **FR-044**: The list of wish lists MUST show per-list summaries only — title, target date, number of
  items, the figures of FR-045 and the participant link — and MUST NOT render any list's entries
  inline (mirrors 007 FR-014b).
- **FR-045**: For every wish list the admin area MUST show, each labelled with what it counts:
  1. the number of entries made;
  2. the number of wished places — the wanted counts of all its items added together;
  3. the filled share: entries divided by wished places, as a percentage;
  4. the number of items that carry no entry at all;
  5. the number of items whose places are all filled.
- **FR-045a**: The filled share of FR-045.3 MUST be computed over places, not over items, so that an
  item wanted three times with one name counts as one third filled rather than as taken. Figure
  FR-045.4 MUST be shown beside it, because it answers the other reading of the same question — how
  many items nobody has claimed at all — and the two MUST be separately labelled so neither is
  mistaken for the other.
- **FR-045b**: A wish list whose every place is filled MUST be stated as complete in words, not only
  as 100 %.
- **FR-046**: The wish-list area MUST distinguish "no wish lists have been created" from "the stored
  data cannot be read right now", in the same manner the poll area does (007 FR-017).
- **FR-047**: The wish-list area MUST NOT contain any control that configures or maintains the
  installation as a whole (007 FR-015), and settings MUST remain the last navigation entry
  (007 FR-018).

#### The dashboard

- **FR-048**: The dashboard MUST report on wish lists in addition to the six figures feature 007
  defines, and the wish-list report MUST be distinguishable from the poll figures rather than mixed
  into them.
- **FR-048a**: The wish-list report MUST be an overview with one row per wish list, each row
  carrying the list's title, its target date, whether it is open or closed, the number of entries
  made, and the filled share (FR-045.3). Every wish list stored MUST appear in it.
- **FR-048b**: FR-048a amends feature 007's FR-034, which forbids any poll title on the dashboard,
  so that it permits **wish-list titles only**. The amendment is narrow and everything else about
  FR-034 stands: no participant display name may appear on the dashboard (FR-050), poll titles stay
  off it, and no new data may be recorded to make the overview possible.
- **FR-048c**: The overview MUST be ordered open lists first by target date with the nearest first,
  then closed lists with the most recently closed first, so that what still needs chasing is at the
  top. It MUST state how many wish lists there are in total, and MUST be bounded in height and
  scroll within itself, so that at the scale of FR-054 it cannot push the dashboard's other figures
  off the screen.
- **FR-048d**: Each row MUST lead to that wish list in the wish-list area in one action. That link
  is navigation, not a control, and does not breach FR-051.
- **FR-049**: Whatever form FR-048a takes, every figure the dashboard shows for wish lists MUST agree
  with what the wish-list area shows for the same data (007 FR-029).
- **FR-050**: The dashboard MUST NOT show any participant's display name, and MUST NOT require
  recording anything that is not recorded already (Principle IV; 007 FR-034).
- **FR-051**: The dashboard reports; it does not act (007 FR-033). The wish-list report MAY lead into
  the wish-list area, but MUST NOT offer creating, editing or deleting a wish list, and no task may
  require passing through the dashboard.
- **FR-052**: When no wish list exists, the dashboard MUST say so in words rather than presenting
  zeros (007 FR-031). When the data cannot be read, it MUST say so and present no wish-list figure at
  all (007 FR-032).
- **FR-053**: The dashboard MUST re-read its wish-list figures when the operator returns to it, so
  that a change made in the wish-list area is reflected without a manual reload (007 FR-030).
- **FR-054**: The supported scale for SC-009 is **200 wish lists holding up to 200,000 entries in
  total**. Beyond it the figures MUST remain correct and only the timing claim ceases to apply. As in
  007 FR-028c this is a documented scale, not an enforced limit: no rule may refuse the creation of a
  wish list on the grounds of how many already exist. The per-list limits of FR-010 are the only
  enforced maxima.

#### Presentation and accessibility

- **FR-055**: All text introduced by this feature MUST come from the translation catalogue, with no
  literal strings in the interface (007 FR-035). German remains the only interface language.
- **FR-056**: Every interactive control on both the admin and the participant surfaces MUST be
  operable by keyboard alone, MUST carry a text label naming what it does, and MUST visibly mark
  keyboard focus (002 FR-050, FR-051, FR-052).
- **FR-057**: Whether an item is open or complete, and how many places remain, MUST be conveyed in
  text and MUST NOT depend on colour alone.
- **FR-058**: The participant page MUST remain readable and usable on a screen 375 pixels wide at the
  limits of FR-010 — 100 items carrying up to 1000 wished places and their
  entries — with the entry form for an item reachable without disproportionate scrolling.

### Key Entities

- **Wunschliste (Wish list)**: An occasion something is needed for. Attributes: title, optional
  description, target date, creation moment, participant token. Owns its items; deleting it destroys
  the items and their entries. Has no expiry and no derived deletion date — its retention outcome is
  the operator's explicit deletion (FR-037, FR-040). Is open or closed, derived from the target date
  against the current moment and never stored (FR-028c).
- **Wunsch (Item)**: One thing wished for within a list. Attributes: name (unique within its list),
  wanted count of at least 1 and at most 50 (default 1), position in the list. The wanted counts of
  a list's items add up to at most 1000 (FR-010). Owns its entries. The wanted count is
  the number of entries it can hold.
- **Zusage (Entry)**: One participant's claim on one item — a display name and the moment it was
  submitted. Carries no identity, no contact detail and no network metadata. An item holds between
  zero and its wanted count of them. Every entry belongs to the submission that created it and is
  withdrawable through that submission's personal token.
- **Submission (personal link)**: The act of claiming one or more items in one go, carrying its own
  unguessable token. It groups the entries it created for the sole purpose of letting their author
  view and withdraw them; it grants nothing else (FR-022c).
- **Status figures**: Per wish list, the counts and the filled share defined in FR-045, derived from
  items and entries and recorded nowhere.
- **Creator Account**: Unchanged. The same single operator identity that owns date polls owns wish
  lists; this feature introduces no second account, no per-list ownership and no participant account.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A participant who opens a wish-list link can claim an item in under 60 seconds, in one
  session, with zero steps between the link and the entry form and without creating anything.
- **SC-002**: An item never holds more entries than are wished for. Across 100 simultaneous
  submissions aimed at a single open place, exactly one entry is stored and the item's entry count
  equals its wanted count.
- **SC-002a**: A participant claiming ten items in one submission is never refused by the rate
  limit, and ten submissions within one hour from one source are accepted before the eleventh is
  refused with a message naming the wait.
- **SC-003**: An operator creates a wish list with five items and their quantities in under two
  minutes from opening the admin area.
- **SC-004**: Every status figure matches a hand count of the same list, in 100 % of checks,
  including immediately after an entry is added, an entry is deleted, an item is removed and a wanted
  count is raised.
- **SC-005**: No wish list, item or entry is ever removed without an explicit operator confirmation:
  over any period of running, the number of wish lists that disappear unprompted is zero.
- **SC-006**: Every edit in US2 leaves the participant link working and the pre-existing entries
  intact — 100 % of edits, except the removal of an item, whose destruction is announced in its
  confirmation before it happens.
- **SC-007**: From the admin area, the operator can tell for every wish list how much of it is still
  open without opening any list.
- **SC-007a**: From the dashboard alone, without entering any area, the operator can name which wish
  list is closest to its target date and least filled.
- **SC-008**: Every wish-list admin address can be reloaded and returns the operator to the same
  place, including a single list's detail (007 SC-006).
- **SC-009**: At the scale of FR-054 — 200 wish lists holding up to 200,000 entries — the dashboard
  has its wish-list report on screen within two seconds of being opened, and the wish-list area
  presents its list within two seconds.
- **SC-010**: On a screen 375 pixels wide, a participant can claim an item on a list of 100 items
  without any content being permanently covered, and the whole flow is completable by keyboard alone.
- **SC-011**: "Nothing is stored" and "the data cannot be read" produce visibly different results in
  the wish-list area and on the dashboard, and neither is ever shown as a zero (007 SC-009).
- **SC-012**: A deleted wish list's link is indistinguishable from a link that never existed: an
  observer holding an old link cannot tell deletion from invalidity.
- **SC-013**: A wish list whose target day has ended accepts zero further entries and zero
  withdrawals, while still showing every item and every name it held, marked as closed; moving the
  target date forward restores both, with no entry lost.
- **SC-014**: A personal link grants exactly the entries it was issued for and nothing else — no
  other entry, no other wish list, no admin function — and withdrawing through it makes the place
  claimable by the next participant without operator involvement.

## Assumptions

- **Items are the operator's to define.** Participants claim items; they do not add, rename or
  propose them. The request describes the operator specifying what is wished for, and letting
  participants extend the list would be a different feature with its own moderation questions.
- **An entry is a name against an item, nothing more.** No note, no quantity per person, no "I bring
  two of these", no comment field. A participant who wants to bring two of something claims two
  places. Principle III rejects the extra fields until a second concrete use appears.
- **Names are labels, never identities.** The display name is treated exactly as feature 002 treats
  it: not verified, not unique, not linked to anything, and visible to everyone holding the link.
- **The filled share is computed over places.** "% der gewünschten Items mit Nutzereintragungen" has
  two readings — the share of wished *places* that are filled, and the share of *items* that anybody
  has claimed. The first is the primary figure because the quantity is what the feature is about; the
  second is reported beside it as the count of untaken items (FR-045.4), so neither reading is lost
  and neither is guessed at.
- **Lowering a count below the entries made is refused rather than resolved.** The alternative —
  accepting the lower count and leaving the item oversubscribed, or dropping the surplus entries —
  either lies about the state or destroys someone's claim without saying so. Refusing names the
  obstacle and leaves the operator to remove the entries deliberately.
- **Capacity is the only thing that bounds entries.** A list holds as many entries as its items
  wish for, and nothing else refuses one. Every limit an operator can hit is enforced where the
  operator works; the participant's only possible refusals are a full item, a closed list, and the
  rate limit.
- **Closed is a reading of the clock, not a stored flag.** There is no "open" or "close" action, no
  state an operator can leave wrong, and nothing to migrate if the target date changes. Principle III
  rejects a stored state whose only correct value is already computable.
- **Wish lists and date polls do not interact.** A wish list is not attached to a poll, does not
  inherit a poll's dates, and a poll's chosen day does not become a wish list's target date. Both are
  independent capabilities of the same installation.
- **No import or export for wish lists.** Features 003 and 005 give polls a per-poll export and a
  per-poll import; nothing in this request asks for the equivalent, and the whole-installation backup
  covers wish-list data by covering everything the installation stores. Adding per-list export is a separate feature.
- **The existing operator account, session and sign-in are reused unchanged.** This feature adds no
  authentication behaviour of its own.
- **Existing behaviour is preserved.** Date polls, settings, maintenance mode, backup and restore
  keep behaving as features 002–007 specified. This feature adds an area beside them.
- **The dashboard's wish-list overview names lists, never people.** That is the whole of the change
  to 007 FR-034. The privacy rule it carried is about participants, and participants stay unnamed on
  the dashboard; a title the operator wrote for themselves is not participant data.
- **Dashboard figures are read on entering the dashboard.** No live updating, polling or push is
  assumed; returning to the dashboard is what refreshes it (007).

## Dependencies

- Feature 007 (admin shell and dashboard) supplies the navigation the "Wunschlisten" entry joins, the
  rules for an area's address and empty states, and the dashboard this feature reports into. This
  feature amends 007 FR-034 narrowly, to permit wish-list titles on the dashboard while leaving the
  rest of that requirement — and the treatment of polls — unchanged (FR-048b).
- Feature 002 (date poll) supplies the patterns this feature reuses deliberately: the unguessable
  participant token, the display-name handling, the indistinguishable response for unknown links, the
  rate limit, the server-side limit enforcement and the fixed Europe/Berlin day boundary.
- Feature 005 (maintenance mode) supplies the state that must also refuse wish-list entries.
- Feature 003 (SQLite and backup) covers wish-list data through the existing whole-installation
  backup; no per-list export is added here.

## Out of Scope

- Participant-proposed items, comments, notes or quantities per person.
- Notifying anyone — no email, no reminder as the target date approaches, no notification when an
  item fills up. The system has no contact detail for any participant and this feature adds none.
- Per-list export and per-list import of wish lists.
- Attaching a wish list to a date poll, or deriving one from the other.
- Any automatic retention or expiry for wish lists; the explicit deletion path is the whole retention
  story by request (FR-037).
- A second operator account, per-list ownership, or any participant account.
- Changes to date polls, settings, maintenance mode, backup or restore beyond adding the new
  navigation entry and the dashboard's wish-list report.
- Enforcing a maximum number of wish lists. FR-054 documents 200 as a supported scale for measuring
  SC-009; refusing lists beyond it would be its own feature.
