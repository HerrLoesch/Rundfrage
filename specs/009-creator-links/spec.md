# Feature Specification: Ersteller-Links (Creator Links)

**Feature Branch**: `009-creator-links`
**Created**: 2026-09-13
**Status**: Draft
**Input**: User description: "Ich will jemandem einen Link erstellen können, über den er selbst wunschlisten und termin findungen anlegen kann. er sollte über einen Namen identifiziert werden. es sollen bis zu 100 solcher links existieren. Sie sollen dabei nur einen link bekommen aber über diesen nur ihre eigenen wunschlisten und terminsuchen sehen. ich soll weiterhin als admin alles sehen können."

## Summary

Rundfrage has exactly two kinds of person in it. One holds the operator password and can do
everything; everybody else holds a participant link and can answer one thing. There is no way to let
somebody else *make* a poll or a wish list without handing them the password to the installation —
which hands them maintenance mode, the backup and the restore along with it.

This feature adds the missing third kind: an **Ersteller** — someone the operator names and hands a
single link to. Through that link, and without an account, a password or a sign-up, they create
their own date polls and their own wish lists, share the participant links those produce, and watch
the answers come in. They see their own and nothing else: not another Ersteller's, not the
operator's. The operator keeps seeing all of it, and keeps being the only one who can touch the
installation itself.

Four properties define the feature and each one is deliberate.

**The name is a label the operator writes, not an identity anybody proves.** The operator creates a
link and calls it "Anna" so that they can tell, six weeks later, which link is whose and who made
the wish list for the summer party. Nobody types that name to get in and nothing verifies it. It is
the same kind of string as a poll title: written by the operator, for the operator.

**The link is the whole authorisation.** This is the pattern the system already runs on — an
unguessable token in a URL, no session to establish, nothing between the link and the work. It is
also the pattern's cost, stated plainly rather than buried: whoever holds the URL *is* that
Ersteller. The constitution permits it (creators MAY be asked to authenticate, participants MUST
NOT), the token is the same 128 bits every participant link already uses, and the operator can
replace a leaked link without destroying what was made through it (FR-016).

**Isolation is a server property, not a filtered view.** An Ersteller asking for another Ersteller's
poll by its identifier gets the answer an unknown link gets — not "forbidden", which would confirm
that it exists. The list they see is the list the server built for their token, and no request they
can construct reaches past it.

**Nothing changes for participants, and nothing changes for the operator's own work.** A poll made
by an Ersteller is an ordinary poll: same participant link, same answer form, same thirty-day
retention. A wish list made by an Ersteller is an ordinary wish list. The admin area gains an
"Ersteller" navigation entry and an owner column; it loses nothing.

## Clarifications

### Session 2026-09-13

- Q: What happens to an Ersteller's polls and wish lists when the operator revokes their link?
  → A: **Revoking invalidates the link and nothing else** (FR-018, FR-020). The Ersteller stays
  listed as a named owner with no working link, keeps everything it owns, and can be given a new link
  later (FR-016). Ending the relationship altogether is a second, explicitly destructive action —
  deleting the Ersteller — which destroys its polls and wish lists with it (FR-020a). The split
  mirrors the one the spec already draws between reissuing a link and revoking it: one action changes
  what a link does, the other changes what exists. Two consequences are accepted rather than hidden.
  First, a revoked Ersteller still occupies a place against the limit of 100 until it is deleted
  (FR-009a) — revocation is not a way to make room. Second, deletion cannot spare the content by
  handing it to the operator, because ownership is fixed at creation and never transfers (FR-012);
  the alternative would have made the owner column say the operator created things they did not,
  which is the kind of statement 008 FR-033 refused to make.
- Q: What does the Ersteller's surface contain — which of the admin area's capabilities does the
  link grant? → A: **The floor of FR-022 to FR-027, plus feature 003's per-poll export of their own
  polls. No per-poll import, and no dashboard** (FR-028, FR-028a, FR-028b). Export is the one
  capability the floor withheld over data the Ersteller demonstrably owns, and withholding it became
  indefensible once FR-020c settled that deleting an Ersteller destroys that data: a holder must be
  able to take out what is theirs before somebody else's decision removes it. The two exclusions are
  refusals, not omissions. Import would put feature 005's upload-and-parse path behind a bearer link
  — the weakest credential in the system — and would require a new rule for who owns an imported
  poll, in exchange for a capability nobody asked for. A scoped dashboard would create a second set
  of figures that FR-047's agreement rule would have to cover, including 008's wish-list overview,
  to answer "what is in here?" for someone who can already see all of their own content on two
  pages. Accepted consequence: an Ersteller who wants an overview reads their two lists.
- Q: May the operator change an Ersteller's polls and wish lists, or only see them?
  → A: **Full parity — the operator may do to any poll or wish list exactly what they may do to one
  of their own** (FR-040). The request says "sehen", but a view-only operator collides with Principle
  IV, which requires every survey to have an operator-reachable deletion path, and it is already
  contradicted by FR-020a. Once destruction is granted, withholding a rename withholds the smaller
  power while granting the larger, and it would put an owner-dependent rule into every admin screen
  for no protection. Accepted consequence, stated because it is real: the operator can change an
  Ersteller's list with no notice to its holder, and this feature adds no audit log to say it
  happened — Principle IV asks for less recorded, not more. The holder sees the current state the
  next time they open their link, and ownership itself never moves (FR-012), so an edited list is
  still theirs.

- Q: What bounds an Ersteller's activity — a rate limit, a cap on how much they may own, or both?
  → A: **A distinct write budget of 60 writes per hour per request source, and no cap on how much an
  Ersteller may own** (FR-036, FR-036a). Reusing the participant budget of 10 per hour was rejected
  on measurement of the task rather than of the risk: adding a tenth item to a wish list is ten
  writes, so the participant budget would refuse an Ersteller in the middle of the first thing they
  were invited to do. A per-Ersteller ownership cap was rejected because creation here is invited
  activity — the operator handed the link over precisely so that things would be created — and
  because the abuse case a cap addresses is a leaked link, which FR-016 and FR-018 already answer by
  taking the link away. Accepted consequence: 100 Erstellern share one installation, so the
  documented scales of 007 FR-028c and 008 FR-054 are now reached by many hands rather than one
  (FR-036b). They are installation-wide totals and do not change; what changes is that nobody owner
  can see how close the installation is to them, which is one more reason the dashboard stays whole
  (FR-041).

- Q: Does the creator surface give each destination its own reloadable address, as feature 007 does
  for the admin area? → A: **No — one address, one page** (FR-028d to FR-028g). Everything the
  holder can reach lives at the token's address: both lists, and a poll's answers or a wish list's
  detail as disclosures that open within that page. This is a deliberate departure from 007 FR-014a
  and 008 FR-043, which made a poll's answers and a wish list's detail destinations of their own
  precisely because they could not otherwise be linked to or survive a reload. The reason the same
  argument does not carry here is that the address carries the credential: every additional address
  is another place the token is written down, in history, in a copied link and in a screenshot, and
  the holder has no password to fall back on when one of them leaks. Two consequences are accepted
  rather than hidden. First, an Ersteller cannot return directly to one poll's answers and the back
  button does nothing useful inside the surface — a cost 007 called out when it moved the admin area
  in the opposite direction, and which is paid here by a holder with two short lists rather than by
  an operator with five hundred polls. Second, a reload returns them to the top of the page and
  discards any form they had open but not confirmed (FR-028f, mirroring 007 FR-014j).
## User Scenarios & Testing *(mandatory)*

### User Story 1 - Hand somebody a link and let them make their own things (Priority: P1)

The operator opens the Ersteller area, creates a link and calls it "Anna", and sends Anna the one
link it produces. Anna opens it on her phone. There is no sign-in, no password and no account: the
page greets her by the name the operator gave the link and shows her, on that one page, two empty
lists — her date polls and her wish lists. She creates a wish list for the summer party with three items, shares the
participant link it produces with the family, and later creates a date poll for the follow-up. When
the poll has filled up she downloads its results for herself. Both behave exactly as the operator's
own do, because they are the same polls and the same wish lists.

**Why this priority**: This is the feature. Without it the operator is the only person in the
installation who can make anything, and the only way to change that is to give away the password.
Everything else in this spec reports on, scopes or revokes what this story produces.

**Independent Test**: Create one Ersteller link, open it in a fresh browser with no session, create
one wish list and one date poll through it, and confirm both produce working participant links that
an unrelated browser can answer.

**Acceptance Scenarios**:

1. **Given** the operator is signed in, **When** they create an Ersteller with a name, **Then** the
   Ersteller is stored and exactly one link is shown, containing an unguessable token.
2. **Given** a valid Ersteller link, **When** it is opened with no session and no account, **Then**
   the holder reaches their own two lists directly, with no sign-in, no password prompt and no step of
   any kind in between.
3. **Given** a valid Ersteller link, **When** the page loads, **Then** the name the operator gave
   the link is shown, so the holder can tell which link they are using.
4. **Given** an Ersteller with no content, **When** they open their link, **Then** each of the two
   lists says it is empty and offers creating the first poll or the first wish list directly.
5. **Given** an Ersteller link, **When** its holder creates a date poll, **Then** the poll is created
   with everything feature 002 requires, and its participant link works for someone holding neither
   the Ersteller link nor the operator password.
6. **Given** an Ersteller link, **When** its holder creates a wish list, **Then** the list is created
   with everything feature 008 requires, and its participant link works for someone holding neither
   the Ersteller link nor the operator password.
7. **Given** an Ersteller's poll, **When** its holder opens it, **Then** they see its answers, its
   results and its per-day summary exactly as the operator would see their own.
8. **Given** an Ersteller's wish list, **When** its holder opens it, **Then** they can edit it and
   see its status figures exactly as the operator would see their own.
9. **Given** an Ersteller's poll or wish list, **When** its holder deletes it, **Then** it is
   destroyed with the same confirmation the operator gets, and no other owner's content is affected.
10. **Given** an Ersteller's own poll, **When** its holder exports it, **Then** they receive the same
    export the operator would receive for a poll of their own.
11. **Given** an Ersteller link, **When** its holder looks for a way to import a poll from a file or
    to upload anything at all, **Then** none is offered and no such route accepts a request from it.
12. **Given** an Ersteller link, **When** its holder looks for a dashboard or a figure summarising
    their content as a whole, **Then** none is offered; their two lists are the whole surface.
13. **Given** an Ersteller link, **When** its holder opens a poll's answers or a wish list's detail,
    **Then** it opens within the same page, the address does not change, and no browser history entry
    is added.
14. **Given** an Ersteller with a form open but not confirmed, **When** they reload their link,
    **Then** they are returned to the top of their surface with both lists shown, the unconfirmed
    form is gone, and everything they had already saved is intact.
15. **Given** an unknown, malformed or revoked Ersteller link, **When** it is opened, **Then** one
    response is produced that does not distinguish between those cases.

---

### User Story 2 - Each link shows its holder their own and nothing else (Priority: P2)

The operator has issued links to Anna and to Ben. Anna's link lists Anna's two wish lists and her
poll. It does not list Ben's, it does not list the operator's, and it does not say that any exist.
Ben's link, opened in a different browser, shows the mirror image. Neither of them can reach the
other's poll by editing the address, and neither can reach settings, maintenance mode, the backup or
the restore by any route at all.

**Why this priority**: The request is explicit — "über diesen nur ihre eigenen wunschlisten und
terminsuchen sehen". A link that leaked one Ersteller's content into another's view would make the
feature unusable for the purpose it was asked for, and a link that reached the installation's
controls would be worse than giving away the password, because nobody would expect it to.

**Independent Test**: Issue two links, create one poll and one wish list through each, then confirm
from each link that only its own two items are listed and that a request naming the other's
identifiers is refused indistinguishably from a request naming something that never existed.

**Acceptance Scenarios**:

1. **Given** two Ersteller links each with content, **When** one is opened, **Then** it lists only
   that Ersteller's polls and wish lists, and the other's appear nowhere, in no count and in no
   figure.
2. **Given** content created by the operator directly, **When** an Ersteller link is opened, **Then**
   none of it is listed and nothing indicates that it exists.
3. **Given** an Ersteller link, **When** its holder requests another owner's poll, wish list, answers
   or status figures by identifier, **Then** the response is the one an unknown identifier produces,
   revealing nothing about whether it exists.
4. **Given** an Ersteller link, **When** its holder attempts to reach maintenance mode, the backup,
   the restore, the settings area, the operator's dashboard, the sign-in form's session or the
   management of Ersteller links, **Then** every one of them is refused.
5. **Given** an Ersteller link, **When** its holder attempts to delete, edit or export another
   owner's poll or wish list, **Then** it is refused and nothing is changed.
6. **Given** an Ersteller link, **When** its holder attempts to read or modify a participant's
   response or entry belonging to another owner's poll or wish list, **Then** it is refused.
7. **Given** an Ersteller's own participant link for their own poll, **When** a participant uses it,
   **Then** the participant surface is exactly what feature 002 specifies and shows no Ersteller
   name, no other content and no way into any creator or admin surface.

---

### User Story 3 - The operator still sees everything, and sees whose it is (Priority: P3)

The operator opens the date-poll area and sees every poll in the installation — their own and every
Ersteller's — each row saying who it belongs to. The same for wish lists. The dashboard counts all
of it, as it always has, because it counts what the installation holds. Nothing the operator could
do before now requires knowing who made something.

**Why this priority**: "ich soll weiterhin als admin alles sehen können" is a requirement to
*preserve* behaviour, and the behaviour it preserves already works. It ranks below the first two
because it is what happens if this feature changes nothing in the admin area — but the owner column
is new, and without it "everything" becomes an undifferentiated pile.

**Independent Test**: With content owned by the operator and by two Erstellern, confirm the admin
poll list and wish-list list show all of it, that each row names its owner, and that the dashboard's
figures equal a hand count across all owners.

**Acceptance Scenarios**:

1. **Given** polls owned by the operator and by two Erstellern, **When** the operator opens the
   date-poll area, **Then** every poll is listed regardless of owner.
2. **Given** wish lists owned by the operator and by two Erstellern, **When** the operator opens the
   wish-list area, **Then** every wish list is listed regardless of owner.
3. **Given** a poll or wish list in the admin area, **When** its row is read, **Then** it names its
   owner — the Ersteller's name, or that it is the operator's own.
4. **Given** content owned by several Erstellern, **When** the operator reads the dashboard, **Then**
   its figures count all of it, and the wish-list overview rows name their owner.
5. **Given** the admin area, **When** the operator opens any poll's answers or any wish list's
   detail, **Then** it opens whoever owns it, at the same address and with the same content as
   before this feature.
6. **Given** an Ersteller's poll, **When** the operator exports or deletes it, **Then** it behaves
   exactly as the operator's own poll does.
7. **Given** an Ersteller's wish list, **When** the operator edits its title, its items or a wanted
   count, **Then** the edit is permitted exactly as it is on the operator's own wish list, and the
   list is still owned by and listed under that Ersteller afterwards.
8. **Given** an Ersteller's poll or wish list, **When** the operator deletes a single response or
   entry from it, **Then** it behaves exactly as on the operator's own, and nothing else changes.
9. **Given** the admin area, **When** any action is offered on a poll or wish list, **Then** the same
   actions are offered whoever owns it, and none is hidden or refused on grounds of ownership.
10. **Given** the operator has edited an Ersteller's wish list, **When** the Ersteller next opens
    their link, **Then** they see the current state, and nothing notified them and nothing recorded
    who made the change.

---

### User Story 4 - Manage the set of links (Priority: P4)

The operator opens the Ersteller area and sees the Ersteller that exist: each with its name, when it
was created, how many polls and wish lists it owns, and — if it still has one — the link itself to
copy again. They add a new one, rename one whose owner changed, replace the link of one that was
forwarded to the wrong chat, and revoke one that is finished, which stops its link without touching
anything it made. Months later, when nobody needs those wish lists any more, they delete that
Ersteller outright and the confirmation tells them exactly how much goes with it. The hundredth
Ersteller is accepted; the hundred-and-first is refused and says so.

**Why this priority**: An installation with links but no way to see or end them is one leaked URL
away from being unfixable. It is last because the first link can be issued and used without any of
it, and because the cap only matters once the links are plentiful.

**Independent Test**: Create Ersteller up to the cap and confirm the next is refused naming the
limit; reissue one link's token and confirm the old URL stops working while its content survives;
revoke one and confirm its URL stops working while its polls and wish lists remain listed and its
participant links keep answering; then delete it and confirm the confirmation named both counts and
that the content is gone.

**Acceptance Scenarios**:

1. **Given** the Ersteller area, **When** it is opened, **Then** every existing Ersteller is listed
   with its name, its creation date, its counts of polls and wish lists, whether it has a working
   link, and that link when it has one.
2. **Given** 100 Ersteller exist, **When** the operator creates another, **Then** it is refused,
   naming the limit of 100, how many exist, and that a place is freed by deleting an Ersteller
   rather than by revoking one.
3. **Given** fewer than 100 exist, **When** the operator creates one, **Then** it is accepted.
4. **Given** an existing Ersteller, **When** the operator renames it, **Then** the new name is shown
   everywhere that Ersteller appears, its link is unchanged, and its content is untouched.
5. **Given** an existing Ersteller, **When** the operator issues a new link for it, **Then** the old
   link stops working immediately, the new one works, and every poll and wish list that Ersteller
   owns is still theirs.
6. **Given** an existing Ersteller, **When** the operator revokes it, **Then** its link stops working
   immediately and behaves as an unknown link, the revocation is confirmed first stating that nothing
   it owns will be removed, and afterwards every poll and wish list it owns is still listed under its
   name.
7. **Given** a revoked Ersteller, **When** the operator issues a new link for it, **Then** the holder
   of that link reaches exactly the polls and wish lists the Ersteller owned before it was revoked.
8. **Given** a revoked or renamed Ersteller, **When** a participant uses a link for one of that
   Ersteller's polls or wish lists, **Then** the participant surface is unaffected.
9. **Given** an Ersteller owning polls and wish lists, **When** the operator deletes it, **Then** the
   confirmation names it and states both counts before anything happens, and on confirming, the
   Ersteller and all of that content are destroyed and its participant links behave as unknown links.
10. **Given** the Ersteller area, **When** revoking and deleting are compared, **Then** they are
    separate, separately labelled actions and only the deletion announces destruction.
11. **Given** 100 Ersteller exist and one is deleted, **When** the operator creates another, **Then**
    it is accepted, and the deleted Ersteller's name is available again.
12. **Given** two Ersteller with the same proposed name, **When** the second is created, **Then** it
    is refused, naming the collision, whether or not the first has been revoked.

---

### Edge Cases

- **The link is forwarded.** Anyone holding the URL is that Ersteller; the design accepts this and
  answers it with FR-016 (issue a new link, keep the content) rather than with a second secret.
- **The link is open when it is revoked.** The next request the holder makes is refused as an unknown
  link; nothing waits for them to close the tab. What they had already created is untouched and is
  still listed under their name in the admin area.
- **A revoked Ersteller's polls and wish lists keep running.** Their participant links still answer,
  their answers still arrive, and a poll of theirs still reaches its retention deadline on schedule.
  Revoking ends one person's access, not the things they made.
- **The operator has hit 100 and revokes some links to make room.** It does not help, and FR-009's
  refusal says so: only deleting an Ersteller frees a place.
- **An Ersteller is deleted while a participant is filling in one of its polls.** The submission is
  refused exactly as a submission to a deleted poll is today (002 FR-040), with no new failure mode.
- **Two browsers on one link.** Nothing prevents it and nothing needs to: there is no session to
  conflict, and the content is the same content.
- **An Ersteller's poll reaches its retention deadline.** It is erased by feature 002's retention
  exactly as the operator's would be. Ownership changes nothing about when a poll dies.
- **An Ersteller's wish list.** It is never erased by time, exactly as feature 008 specifies.
  Ownership changes nothing about that either.
- **Maintenance mode is switched on.** The creator surface is refused with the same notice the
  participant surface shows, because a restore about to replace every row must not be racing someone
  creating a poll.
- **A restore replaces the installation.** The Ersteller links in the restored file are the links
  that exist afterwards; a link issued after the backup was taken stops working, as everything else
  created after it does.
- **The backup's restore preview.** It must count Ersteller links too, or it understates what a
  restore destroys — the same defect feature 008 recorded for wish lists.
- **An Ersteller creates up to the limits.** Every per-poll and per-wish-list limit of features 002
  and 008 applies unchanged; the Ersteller is refused by the same rules the operator is.
- **The operator deletes content an Ersteller made.** It is destroyed, with the same confirmation;
  the Ersteller's list simply no longer shows it.
- **A name with no content.** An Ersteller who has made nothing is listed with zeros, not omitted.
- **The holder bookmarks their surface.** The bookmark is the Ersteller link, which is the only
  address the surface has. It always works until the link is revoked or replaced.
- **The holder wants to return to one poll's answers.** They cannot link to it; they open their link
  and open the poll again. This is the accepted cost of FR-028d, and FR-028g is what keeps it cheap.
- **The holder shares their screen or a screenshot.** Exactly one address can expose the token, and
  FR-058 has already told them what the link grants.

## Requirements *(mandatory)*

### Functional Requirements

#### The Ersteller and their link

- **FR-001**: An Ersteller MUST have a name, given by the operator. Creation without one MUST be
  refused, naming what is missing.
- **FR-002**: The name MUST be at most 100 characters and MUST be distinct among the Ersteller that
  exist, revoked ones included, since a revoked Ersteller still exists and still owns content
  (FR-020). A duplicate MUST be refused and MUST name the collision. Deleting an Ersteller (FR-020a)
  MUST release its name for reuse.
- **FR-003**: The name is a label the operator writes for the operator. It MUST NOT be typed by
  anybody to gain access, MUST NOT be verified, and MUST NOT be treated as an identity or a contact
  detail. No email address, no telephone number and no other contact information may be recorded for
  an Ersteller (Principle IV).
- **FR-004**: On creation the system MUST generate exactly one Ersteller link containing an
  unguessable token, drawn from the same space as every other capability token in the system and
  large enough that guessing a valid link is not a practical attack (002 FR-016, FR-017).
- **FR-005**: One Ersteller MUST have at most one valid link at a time — exactly one until it is
  revoked, none afterwards until a new one is issued (FR-016, FR-018). The system MUST NOT issue a
  second concurrent link for the same Ersteller.
- **FR-006**: Opening a valid Ersteller link MUST require no account, no sign-in, no password, no
  email address, no confirmation and no installation of anything, and no step of any kind may be
  placed between the link and the holder's own two lists.
- **FR-007**: The creator surface MUST show the Ersteller's name, so that the holder can tell which
  link they hold and the operator can describe it to them.
- **FR-008**: An unknown, malformed or revoked Ersteller link MUST produce one response that does not
  distinguish between those cases (002 FR-027; 008 FR-024).
- **FR-009**: At most 100 Ersteller MUST exist at any one time. Creating one beyond that MUST be
  refused, stating the limit and how many exist. This is an enforced maximum, not a documented scale
  in the sense of 007 FR-028c or 008 FR-054: the request states it as a bound on the system, and the
  operator meets it where they act.
- **FR-009a**: Deleting an Ersteller (FR-020a) MUST free its place against the limit of FR-009.
  Revoking one MUST NOT: a revoked Ersteller still exists and still owns its content, so it still
  occupies a place. The refusal of FR-009 MUST therefore say that a place is freed by deleting an
  Ersteller, not by revoking one, so that an operator who has hit the limit is not left revoking
  links to no effect.
- **FR-010**: Creating, renaming, reissuing, revoking and listing Ersteller MUST require an
  authenticated operator session and MUST be refused otherwise without revealing whether the named
  Ersteller exists (002 FR-001, FR-002).

#### Ownership

- **FR-011**: Every date poll and every wish list MUST have exactly one owner: either the operator or
  one Ersteller.
- **FR-012**: Ownership MUST be fixed when the poll or wish list is created — by whoever created it —
  and MUST NOT be changeable afterwards by anyone. There is no transfer, no sharing and no second
  owner.
- **FR-013**: Every date poll and every wish list that exists before this feature MUST become the
  operator's own, and MUST behave in every respect as it did before.
- **FR-014**: Ownership MUST NOT change any property of a poll or a wish list other than who may see
  and act on it. In particular it MUST NOT change the participant link, the participant surface, the
  answer or entry flow, the limits of features 002 and 008, a poll's thirty-day retention, or a wish
  list's absence of expiry.
- **FR-015**: An Ersteller's name MUST NOT appear on any participant surface. A participant holding a
  poll or wish-list link MUST NOT be able to tell who created it, nor that Ersteller links exist
  (Principle IV).

#### Replacing and revoking a link

- **FR-016**: The operator MUST be able to issue a new link for an existing Ersteller. The previous
  link MUST stop working immediately and MUST behave as an unknown link (FR-008); every poll and wish
  list that Ersteller owns MUST remain theirs and MUST be unchanged. This is the remedy for a link
  that has been forwarded or leaked, and it is the reason the design can accept a bearer link at all.
- **FR-017**: The operator MUST be able to rename an Ersteller. The link MUST be unchanged, the
  content MUST be unchanged, and the new name MUST be what is shown everywhere that Ersteller appears
  from then on.
- **FR-018**: The operator MUST be able to revoke an Ersteller. The link MUST stop working
  immediately and MUST behave as an unknown link (FR-008).
- **FR-019**: Revocation MUST require an explicit confirmation that names the Ersteller and states
  that its link will stop working and that nothing it owns will be removed.
- **FR-020**: Revocation MUST NOT remove, alter or reassign any poll, wish list, answer or entry the
  Ersteller owns, and MUST NOT affect any participant link those produce. The Ersteller MUST remain
  listed in the Ersteller area, MUST remain the named owner of its content wherever the operator sees
  it (FR-039), and MUST be able to receive a new link at any time (FR-016), which restores its
  holder's access to exactly what it owned before.
- **FR-020a**: The operator MUST additionally be able to **delete** an Ersteller. Deletion MUST
  destroy the Ersteller together with every poll and wish list it owns, and every answer and entry
  within those, and MUST require an explicit confirmation naming the Ersteller and stating both
  counts before it happens (008 FR-034, FR-038). Afterwards every link those polls and wish lists
  produced MUST behave as an unknown link (002 FR-040; 008 FR-039).
- **FR-020b**: Deletion MUST NOT be offered as a variation of revocation or reachable by mistake from
  it. The two MUST be separate, separately labelled actions, and the interface MUST make plain which
  one destroys content.
- **FR-020c**: Deleting an Ersteller MUST NOT hand its content to the operator or to any other
  Ersteller. Ownership is fixed at creation and never transfers (FR-012); an operator who wants to
  keep something an Ersteller made MUST export it beforehand, and the confirmation of FR-020a MUST
  say so.
- **FR-021**: No Ersteller may revoke, rename, reissue, delete or list any Ersteller, including their
  own. This is the management of Ersteller links, which FR-029 already places out of reach; it is
  restated here only so the prohibition is findable from the section that creates them, and FR-029
  remains the single statement that governs.

#### What an Ersteller may do

- **FR-022**: An Ersteller MUST be able to create date polls, with everything feature 002 requires of
  a poll and subject to every limit feature 002 enforces.
- **FR-023**: An Ersteller MUST be able to create wish lists, with everything feature 008 requires of
  a wish list and subject to every limit feature 008 enforces.
- **FR-024**: An Ersteller MUST be able to see, for the polls they own: the list of them, each poll's
  participant link, its answers, its results and its per-day summary — everything the operator sees
  for a poll of their own (002, 004, 006).
- **FR-025**: An Ersteller MUST be able to see, for the wish lists they own: the list of them, each
  list's participant link, its items, its entries and its status figures — everything the operator
  sees for a wish list of their own (008 FR-043, FR-045).
- **FR-026**: An Ersteller MUST be able to edit the wish lists they own, exactly as feature 008
  permits the operator to (008 FR-029 to FR-034).
- **FR-027**: An Ersteller MUST be able to delete the polls and wish lists they own, and to delete an
  individual response or entry within them, with the same confirmations the operator receives
  (002 FR-037a, FR-038; 008 FR-038, FR-043a).
- **FR-028**: An Ersteller MUST be able to export a poll they own, producing the same export
  feature 003 produces for the operator, so that they can take out what is theirs before anybody
  else's decision removes it (FR-020a, FR-020c). The export MUST be refused for a poll they do not
  own, indistinguishably from a poll that does not exist (FR-034).
- **FR-028a**: An Ersteller MUST NOT be able to import a poll. Feature 005's per-poll import remains
  an operator capability, and no route reachable with an Ersteller link may accept an uploaded file
  of any kind.
- **FR-028b**: The creator surface MUST NOT offer a dashboard or any aggregate figure across that
  Ersteller's own content. Its two lists are the whole of what it shows, and each list carries the
  per-poll and per-wish-list figures features 002, 004 and 008 already define.
- **FR-028c**: FR-022 to FR-028g are the whole of what an Ersteller link grants. Any admin capability
  not named there MUST be refused through it, including ones added by later features until those
  features decide otherwise.
- **FR-028d**: The creator surface MUST live at exactly one address — the Ersteller link itself. Both
  lists MUST be on that page, and a poll's answers and a wish list's detail MUST open as disclosures
  within it rather than as addresses of their own. The token MUST NOT appear in any second address.
- **FR-028e**: Opening a disclosure, closing it and moving between the two lists MUST NOT change the
  address and MUST NOT add a browser history entry, so that no intermediate state of the surface is
  recorded anywhere the token could be read from later.
- **FR-028f**: Reloading the Ersteller link MUST return the holder to the top of their surface with
  both lists shown, and MUST discard any form that was open but not confirmed (007 FR-014j). Nothing
  the holder had already saved may be affected.
- **FR-028g**: Because a poll's answers cannot be linked to on this surface, the lists MUST carry
  enough per-poll and per-wish-list summary for the holder to find what they are looking for without
  opening each one in turn (002, 004, 008 FR-044, FR-045).
- **FR-029**: An Ersteller MUST NOT be able to reach, by any route, any control that configures or
  maintains the installation: maintenance mode, the backup download, the restore, the settings area,
  or the management of Ersteller links (007 FR-015).
- **FR-030**: An Ersteller MUST NOT be able to reach the operator's dashboard or any figure that
  aggregates across owners.
- **FR-031**: Holding an Ersteller link MUST NOT establish an operator session, and MUST NOT make any
  operator-only request succeed.

#### Isolation

- **FR-032**: Every list, count, figure and export the creator surface produces MUST be built from
  only the polls and wish lists that Ersteller owns. Content owned by another Ersteller or by the
  operator MUST NOT appear in it, MUST NOT be counted in it, and MUST NOT be indicated to exist.
- **FR-033**: Scoping MUST be enforced where the data is read, not by filtering a full result for
  display. No request an Ersteller can construct — by naming an identifier, by paging, by sorting, by
  searching or by any parameter — may return content they do not own.
- **FR-034**: A request from an Ersteller naming content they do not own MUST produce the same
  response as a request naming content that does not exist. It MUST NOT distinguish "not yours" from
  "no such thing" (002 FR-002).
- **FR-035**: An Ersteller MUST NOT be able to read or alter a participant's response or entry that
  belongs to content they do not own, through any participant, personal or creator route.
- **FR-036**: Requests through an Ersteller link that create, change or delete something MUST be
  rate-limited to at most 60 per hour per request source. The budget is deliberately separate from
  and larger than the participant budget of 10 per hour (002 FR-027): a participant submits once,
  while an Ersteller building a wish list with ten items spends ten writes on the first task they
  were invited to perform. A refused request MUST say that too many changes were sent and that it can
  be retried later (002 FR-027c), and MUST NOT have changed anything.
- **FR-036a**: No limit MUST be placed on how many polls or wish lists one Ersteller may own. The
  per-poll and per-wish-list limits of features 002 and 008 remain the only enforced maxima on
  content, and the cap of 100 on Ersteller (FR-009) remains the only enforced maximum this feature
  adds.
- **FR-036b**: The supported scales of 007 FR-028c (500 polls, up to 2,500,000 day-answers) and
  008 FR-054 (200 wish lists, up to 200,000 entries) are installation-wide totals and MUST remain
  unchanged. This feature MUST NOT introduce a per-owner scale: at 100 Erstellern those same totals
  are reached by many hands rather than one, and every figure MUST stay correct beyond them, with
  only the timing claims ceasing to apply (007 FR-028c, 008 FR-054).
- **FR-036c**: Reading through an Ersteller link MUST NOT be limited beyond whatever limiting the
  installation already applies to reads, so that a holder refreshing their own list is never refused.
- **FR-036d**: The request source MUST be used for rate limiting only transiently, in memory, and
  MUST NOT be stored, logged or associated with any Ersteller, poll, wish list, answer or entry
  (002 FR-027a, FR-027b; Principle IV).
- **FR-037**: An Ersteller token MUST NOT be written to any log, and MUST NOT appear in any error
  message, any export or any participant-facing page (existing log-redaction rule).

#### What the operator sees

- **FR-038**: The operator's date-poll area MUST list every poll in the installation regardless of
  owner, and the operator's wish-list area MUST list every wish list regardless of owner
  (007, 008 unchanged in every other respect).
- **FR-039**: Every poll and every wish list shown to the operator MUST name its owner — the
  Ersteller's name, or that it is the operator's own — wherever it is listed and on its own detail
  destination.
- **FR-040**: The operator MUST be able to do to any poll or wish list exactly what they can do to
  one of their own — view it, open its answers or entries, export it, edit it where features 002 and
  008 permit editing, delete a single response or entry from it, and delete it entirely — regardless
  of who owns it. No admin action may be refused, hidden or altered on the grounds that an Ersteller
  owns the thing it acts on.
- **FR-040a**: Ownership MUST NOT change when the operator acts on an Ersteller's content. An edited
  or partly emptied poll or wish list MUST still be owned by the same Ersteller and MUST still be
  listed under its name (FR-012, FR-039).
- **FR-040b**: The system MUST NOT notify an Ersteller that the operator changed or deleted something
  they own, and MUST NOT record who performed an admin action. The holder sees the current state when
  they next open their link. This is a deliberate refusal: a notification needs a contact detail the
  system does not hold (FR-003), and an audit trail records more than the feature needs
  (Principle IV).
- **FR-041**: The dashboard's figures MUST continue to count everything the installation holds,
  across all owners, and MUST NOT be split per Ersteller. Its wish-list overview rows (008 FR-048a)
  MUST additionally name each list's owner.
- **FR-042**: An Ersteller's name is operator-written text and MAY be shown on the dashboard on the
  same grounds wish-list titles are (008 FR-048b). No participant display name may appear there
  (008 FR-050), and poll titles stay off it (007 FR-034).
- **FR-043**: Ersteller MUST have their own admin area with its own address, reached from a
  navigation entry labelled **"Ersteller"**, placed in the middle section of the navigation — after
  "Dashboard", before "Einstellungen" (007 FR-002a, FR-003, FR-004, FR-005). Unlike the creator
  surface (FR-028d), this area follows 007's addressing rules unchanged: it is reached by an operator
  holding a session, not by a token in a URL, so nothing is exposed by giving it an address. It is a capability of the installation, not
  a setting of it, and settings MUST remain the last navigation entry (007 FR-018).
- **FR-044**: The Ersteller area MUST show, for every Ersteller: the name, the creation date, the
  number of polls owned, the number of wish lists owned, whether it currently has a working link, and
  — when it has one — the link itself, presented so that it can be copied without being retyped.
- **FR-044a**: A revoked Ersteller MUST be listed alongside the others and MUST be stated in words to
  have no working link, with its counts still shown, so that the operator can see what it still owns
  before deciding whether to give it a new link or delete it.
- **FR-045**: Creating an Ersteller MUST be an action that reveals its form when chosen rather than a
  form that is permanently open, and the area MUST open on the list (007 FR-014h).
- **FR-046**: When no Ersteller exists, the area MUST say so and offer creating one directly from
  that empty state (007 FR-014l), and MUST distinguish "none exist" from "the stored data cannot be
  read right now" (007 FR-017).
- **FR-047**: Every figure the Ersteller area shows MUST agree with what the poll and wish-list areas
  show for the same data (007 FR-029).

#### Existing behaviour that must not change

- **FR-048**: The participant surfaces of date polls and wish lists MUST be unchanged in every
  respect: the same links, the same forms, the same limits, the same rate limits, the same
  indistinguishable refusals, and no navigation towards any creator or admin surface
  (Principle I; 002 FR-021; 007 FR-009; 008 FR-026).
- **FR-049**: The operator's sign-in, session and password MUST be unchanged. This feature adds no
  second password, no user table and no user management.
- **FR-050**: While maintenance mode is on, the creator surface MUST be refused with the notice the
  participant surface already uses, and nothing may be created, edited or deleted through it
  (feature 005). The Ersteller area in the admin shell is not affected, as no admin area is.
- **FR-051**: The whole-installation backup MUST include the Ersteller and their tokens, so that a
  restore returns the installation to a state in which the same links work.
- **FR-052**: The restore preview MUST count the Ersteller it will destroy alongside the polls,
  answers and wish lists it already counts, or it understates what a restore destroys (the defect
  008 recorded for wish lists in its research R-10).
- **FR-053**: A poll's per-poll export and per-poll import (features 003 and 005) MUST continue to
  work for the operator over any poll regardless of owner. A poll the operator imports MUST be owned
  by the operator, since importing is an operator capability and no Ersteller can perform it
  (FR-028a).

#### Presentation and accessibility

- **FR-054**: All text introduced by this feature MUST come from the translation catalogue, with no
  literal strings in the interface (007 FR-035). German remains the only interface language.
- **FR-055**: Every interactive control on the creator surface and in the Ersteller area MUST be
  operable by keyboard alone, MUST carry a text label naming what it does, and MUST visibly mark
  keyboard focus (002 FR-050, FR-051, FR-052).
- **FR-056**: The creator surface MUST remain readable and usable on a screen 375 pixels wide, since
  an Ersteller is as likely to work from a phone as a participant is.
- **FR-057**: Whether a link is the current one or has been replaced MUST be conveyed in text and
  MUST NOT depend on colour alone.
- **FR-058**: The creator surface MUST make plain, before the holder shares anything, that whoever
  holds the Ersteller link can do everything the holder can do — so that a link is not forwarded in
  the belief that it is read-only.

### Key Entities

- **Ersteller (Creator)**: A named link-holder who may create polls and wish lists of their own.
  Attributes: name (distinct among all that exist, operator-written, at most 100 characters), creation
  moment, and at most one current link token — present until the Ersteller is revoked, absent
  afterwards until a new one is issued. Having a link or not is the only state an Ersteller has, and
  it is not a lifecycle: a revoked Ersteller is an ordinary Ersteller that nobody can currently reach.
  Carries no password, no contact detail and no network metadata. At most 100 exist (FR-009), revoked
  ones included. Owns polls and wish lists; revocation leaves them untouched (FR-020) and deletion
  destroys them with it (FR-020a).
- **Owner**: The relationship between a poll or a wish list and whoever created it — exactly one of
  the operator or one Ersteller, fixed at creation and never transferred (FR-011, FR-012). Everything
  stored before this feature is the operator's (FR-013).
- **Terminfindung (Date poll)**: Unchanged in every respect except that it now names an owner.
  Retention, limits, participant link and participant surface are feature 002's and stay so.
- **Wunschliste (Wish list)**: Unchanged in every respect except that it now names an owner.
  Retention, limits, participant link and participant surface are feature 008's and stay so.
- **Creator Account (operator)**: Unchanged. The single operator identity configured at deployment.
  This feature introduces no second password and no account for anybody.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person handed an Ersteller link can create their first wish list or date poll in
  under two minutes, in one session, with zero steps between the link and the creation form and
  without creating an account or entering a password.
- **SC-002**: With content owned by the operator and by two Erstellern, every list, count and figure
  on each Ersteller's surface equals a hand count of that Ersteller's own content, in 100 % of
  checks, and contains zero items belonging to anybody else.
- **SC-003**: For every identifier belonging to another owner — poll, wish list, answer, entry,
  status figure — a request made through an Ersteller link produces a response indistinguishable from
  the response for an identifier that never existed. Zero responses reveal existence.
- **SC-004**: Zero installation-wide controls are reachable through an Ersteller link: maintenance
  mode, backup, restore, settings, poll import and Ersteller management are refused on every route,
  including direct requests that skip the interface. Zero routes reachable with an Ersteller link
  accept an uploaded file.
- **SC-004a**: An Ersteller can export every poll they own and zero polls they do not, and the export
  of a poll they own is byte-for-byte the export the operator receives for the same poll.
- **SC-005**: The operator can see, for every poll and every wish list in the installation, who owns
  it, without opening it.
- **SC-005a**: Every admin action available on a poll or wish list the operator owns is available, and
  behaves identically, on one an Ersteller owns — 100 % of actions, with zero refusals attributable to
  ownership.
- **SC-005b**: After any admin action short of deletion, the poll or wish list is owned by the same
  Ersteller as before: across every edit path, the number of items whose owner changed is zero.
- **SC-006**: The 100th Ersteller is created successfully and the 101st is refused with a message
  naming the limit; revoking one does not make the next creation succeed, and deleting one does.
- **SC-006a**: Revoking an Ersteller destroys nothing: across any number of revocations, the number
  of polls, wish lists, answers and entries in the installation is unchanged, and every participant
  link those polls and wish lists produced keeps working.
- **SC-006b**: No poll or wish list owned by an Ersteller is destroyed without a confirmation that
  stated its count beforehand — 100 % of deletions, counted over the deletion of an Ersteller as well
  as the deletion of single polls and lists.
- **SC-007**: Issuing a new link for an Ersteller makes the previous URL indistinguishable from one
  that never existed, within one request, while 100 % of that Ersteller's polls and wish lists remain
  theirs and unchanged — zero items lost and zero gained, whether or not the Ersteller had been
  revoked first.
- **SC-008**: Every participant link created before this feature and every participant flow it serves
  behaves identically after it: the end-to-end participant tests pass unchanged.
- **SC-009**: Every poll and wish list that existed before this feature is shown to the operator
  afterwards, is owned by the operator, and has lost no answer, entry or link.
- **SC-010**: At 100 Erstellern, and at the documented scales of 007 FR-028c and 008 FR-054 — which
  this feature leaves unchanged as installation-wide totals — an Ersteller's own lists appear within
  two seconds of opening their link, and the operator's poll and wish-list areas stay within the
  two-second budget those features already set.
- **SC-010a**: An Ersteller creating a wish list with ten items and then editing it is never refused
  by the rate limit; 60 writes within one hour from one source are accepted before the 61st is
  refused with a message naming the wait, and the refused request changes nothing.
- **SC-011**: On a screen 375 pixels wide, an Ersteller can create a wish list with five items and
  copy its participant link without any content being permanently covered, and the whole flow is
  completable by keyboard alone — including with both lists at the scale a single Ersteller is
  expected to reach.
- **SC-011a**: Exactly one address on the creator surface contains the token. Over a session that
  opens both lists, opens a poll's answers, opens a wish list's detail and closes them again, the
  number of distinct addresses visited is one and the number of browser history entries added is
  zero.
- **SC-012**: "No Ersteller exists" and "the data cannot be read" produce visibly different results in
  the Ersteller area, and neither is ever shown as a zero (007 SC-009).
- **SC-013**: No Ersteller token appears in any log line, error message or export, over a run that
  exercises every creator route including its refusals.

## Assumptions

- **The link is the authorisation, and its holder is the Ersteller.** This mirrors every other
  capability in the system and is what lets FR-006 hold. The consequence is stated rather than
  mitigated with a second secret: forwarding the URL forwards the capability. FR-016 is the answer —
  the operator replaces the link and keeps the content — and FR-058 makes sure the holder knows it
  before they forward anything.
- **An Ersteller is a label, never a person the system knows.** No email address, no password, no
  profile, no last-seen. The name exists so the operator can tell their links apart.
- **No self-service.** Nobody requests an Ersteller link; the operator creates every one of them.
  There is no invitation flow, no acceptance step and no way to ask for access.
- **One owner, forever.** A poll or wish list belongs to whoever made it. Transferring ownership,
  sharing a list between two Erstellern and co-editing are all rejected under Principle III until a
  second concrete use appears. This is what forces FR-020a to be destructive: there is nowhere for a
  deleted Ersteller's content to go.
- **Revoking and deleting are different questions.** "This person should not be able to get in any
  more" and "this person and everything they made should be gone" are asked at different times and
  answered by different actions. Collapsing them into one would mean either that ending access
  destroys data silently, or that data can only be removed by leaving access open. Accepted cost: two
  actions where a smaller system would have one, and a revoked Ersteller that still occupies one of
  the hundred places until it is deleted.
- **A revoked Ersteller is not a deleted one, and is not hidden.** It stays in the list with its
  counts, because the operator's next decision — new link, or delete — needs exactly those figures.
- **Erstellern are equal to each other and lesser than the operator.** There is no hierarchy among
  them, no Ersteller who may see another, and no partial operator. The system has exactly three
  levels: participant, Ersteller, operator.
- **The operator is unrestricted, and this is a trust assumption, not an oversight.** Whoever holds
  the operator password already holds the restore, which replaces everything. Giving them parity over
  an Ersteller's content adds no power they lacked; refusing it would only make the admin screens
  behave inconsistently. The cost — silent edits with no audit trail — is accepted on the same
  grounds Principle IV uses elsewhere: recording who did what would store more than the feature
  needs, for the benefit of a party the system cannot contact anyway.
- **Existing content belongs to the operator.** FR-013 is the only sensible reading: the operator is
  the only person who could have created it.
- **Retention is a property of the thing, not of its owner.** A poll dies thirty days after its last
  candidate day whoever made it; a wish list dies only when somebody deletes it whoever made it.
  Ownership is not a retention rule and FR-020 is about revocation, not expiry.
- **Creation is invited, so it is paced rather than capped.** An Ersteller exists because the
  operator wanted things created. The rate limit exists to bound what one leaked link can do in an
  hour, not to ration a legitimate holder, which is why the budget is six times the participant's and
  why nothing counts how much they have accumulated. The remedy for a link being misused is to take
  the link away (FR-016, FR-018), not to slow every holder down.
- **The 100 is a real limit.** Unlike the supported scales of 007 and 008, the request states it as a
  bound and FR-009 enforces it, because the operator meets it at the moment they act and can free a
  place by revoking one.
- **The creator surface reuses the shapes that exist.** The poll list, the poll answers destination,
  the wish-list list and the wish-list detail are the same screens with a smaller set of rows; this
  feature does not design a second way of showing a poll.
- **An Ersteller can always take their own data out, and can never put a file in.** Export is granted
  because the data is theirs and FR-020a can destroy it; import is refused because it would place a
  file-parsing path behind the weakest credential in the system. The asymmetry is deliberate and is
  not an oversight in either direction.
- **"Ersteller" is the word, everywhere.** The navigation entry, the area, the owner column and the
  spec all use it. It is German, it is plural, and it names the person rather than the mechanism,
  which is what the request's "über einen Namen identifiziert" asks for.
- **The credential is in the address, so there is only one address.** Feature 007 gave every admin
  destination an address because an operator holding a session loses nothing by it. A holder whose
  whole authorisation is the URL loses something with each additional one, so the creator surface
  takes the opposite decision from the same premise rather than copying 007 out of habit.
- **Two lists are the whole overview.** An Ersteller sees their polls and their wish lists, each with
  the figures those features already define. Nothing sums across the two, because a person with two
  short lists in front of them does not need a third page telling them how many are on each.
- **The operator's dashboard stays installation-wide.** It answers "what is in here"; splitting it
  per Ersteller would answer a question nobody asked, and FR-041 keeps it whole while FR-039 puts the
  owner where the operator is already looking.
- **Maintenance mode covers the creator surface.** It writes to the same storage a restore replaces,
  so it is refused with the participant surface rather than allowed with the admin area.
- **Existing behaviour is preserved.** Date polls, wish lists, the dashboard, settings, maintenance
  mode, backup and restore keep behaving as features 002–008 specified, with the additions this spec
  names and nothing else.

## Dependencies

- Feature 002 (date poll) supplies the unguessable capability token, the indistinguishable response
  for unknown links, the rate limit, the server-side limit enforcement and the rule that a refusal
  must not reveal existence — all of which this feature reuses for the Ersteller link and its
  isolation.
- Feature 007 (admin shell and dashboard) supplies the navigation the "Ersteller" entry joins, the
  rules for an area's address, its empty states and its error states, and the dashboard this feature
  leaves installation-wide.
- Feature 008 (wish list) supplies the second thing an Ersteller can create, and the precedent that
  operator-written text may appear on the dashboard while participant names may not.
- Feature 005 (maintenance mode) supplies the state that must also refuse the creator surface.
- Feature 003 (SQLite, backup and restore) covers Ersteller data through the whole-installation
  backup, and supplies the restore preview that FR-052 extends.

## Out of Scope

- A password, an account or any second credential for an Ersteller. The link is the whole of it.
- Self-service: requesting a link, registering, inviting, accepting an invitation.
- Any hierarchy among Erstellern, any Ersteller who can see another, and any partial operator.
- Transferring ownership, sharing one poll or wish list between owners, and co-editing.
- Expiring an Ersteller link by time. Links end when the operator revokes or replaces them.
- Notifying anybody of anything — no email when a link is issued, revoked or used. The system holds
  no contact detail for an Ersteller and this feature adds none.
- Per-poll import for an Ersteller, and any route reachable with an Ersteller link that accepts an
  uploaded file (FR-028a).
- A dashboard, or any aggregate figure, on the creator surface (FR-028b).
- Per-Ersteller quotas on how many polls or wish lists they may own (FR-036a). The per-poll and
  per-list limits of features 002 and 008 are the only enforced maxima on content, and a quota would
  penalise the activity the link was handed over to enable.
- Changing the installation-wide supported scales of 007 FR-028c and 008 FR-054, or splitting them
  per owner (FR-036b).
- An audit log of what each Ersteller did. Principle IV asks for less recorded, not more.
- Splitting the dashboard per Ersteller, or giving the operator a per-Ersteller report beyond the
  counts of FR-044.
- Changes to the participant surfaces of date polls or wish lists.
