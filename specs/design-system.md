# The layout and type system

**Applies to**: the whole application, not one feature | **Added**: 2026-09-13

Every surface used to decide its own measurements. Six container widths across twelve routed
views, three sizes for a page title, four different "gap before the next block", two mechanisms
for labelling a form field, and cards that were variously elevated, outlined or neither. Nothing
was individually wrong, and the whole read as scattered — the eye measures a layout against
itself, so a 24px gap is correct only if the 24px gap above it was one too.

The decisions now live in two files and the components spend them.

- **`frontend/src/styles/app.css`** — the spacing scale, the two content widths, the type scale and
  the handful of shared arrangements (`rf-meta`, `rf-field-row`, `rf-tile`, `rf-address`).
- **`frontend/src/vuetify.ts`** — the component defaults: field variant and density, card border,
  dialog width.
- **`frontend/src/components/layout/PageHeader.vue`** — the top of every routed view.

---

## The one thing that was actually broken

Vuetify's precompiled stylesheet ships the layout utilities (`d-flex`, `text-medium-emphasis`,
`font-weight-medium`) but **not the typography scale**. `.text-h3`, `.text-h4`, `.text-body-2`,
`.text-subtitle-1` and `.text-overline` are generated from SASS, and this project consumes the
plain CSS build — so all seventy-five typography classes already written across these components
resolved to nothing.

Every heading, label and caption in the application rendered at 16px in weight 400. An `h1` looked
like a heading only because browsers style `h1`. This is most of why the interface read as
unfinished, and no amount of spacing would have fixed it.

The scale is supplied explicitly in `app.css` rather than by switching the build to SASS: the
existing markup keeps working unchanged, and the scale becomes this project's own rather than a
framework default nobody chose. It is tightened one step from Material's, which are drawn for
marketing pages and leave an admin table looking shouty.

Verify it with the computed style, not by eye — a class that does nothing looks exactly like a
class that was never applied:

```js
getComputedStyle(document.querySelector('.text-h3')).fontSize  // must not be 16px
```

---

## The scale

Four steps, no more. A fifth would be a decision every component would then have to make again.

| Token | Value | Spent on |
|---|---|---|
| `--rf-page-y` | 40px / 24px below 600px | A page's outer breathing room |
| `--rf-section` | 32px | Between top-level sections of one page |
| `--rf-stack` | 16px | Between sibling cards in a list |
| `--rf-heading` | 16px | Between a heading and what it introduces |
| `--rf-card-pad` | 24px / 16px below 600px | Inside a card |

Two content widths, one per surface — not one per page. An operator moving between admin areas
sees the content column stay where it was instead of jumping by two hundred pixels.

| Token | Value | Surface |
|---|---|---|
| `--rf-width-admin` | 1120px | Everything inside `AdminShell` |
| `--rf-width-participant` | 960px | `/u/:token`, `/w/:token`, `/z/:token` |

Sign-in keeps its own narrow centred column; it is a single card on an empty page, not a surface.

---

## Rules the components follow

- **One page header.** `PageHeader.vue` draws the title, an optional badge beside it, an optional
  subtitle and an `actions` slot. It wraps rather than using `justify-space-between` on a fixed
  pair — that arrangement pushed the action off the edge at 375px (008 FR-058).
- **One heading level per depth.** Page title `text-h4`, section `text-h5`, card title `text-h6`,
  figure label `text-overline`. `rf-title` adds the weight and the tightening.
- **A card is bordered, never shadowed.** Shadows stack: a card inside a card inside an alert drew
  three overlapping glows and no hierarchy. A border draws one line whatever it is nested in.
- **A field is labelled by its own floating label.** Never a `v-label` above the box for some
  fields and a floating label inside it for others in the same form.
- **A list row is one padded block**, with its controls grouped at the end. Not three card zones
  with a `v-spacer` throwing the actions to opposite edges of an 1120px page.
- **Danger is marked, not painted.** `color="error"` on a card floods it, and the warning and info
  alerts inside then render orange-on-red and blue-on-red. Border and heading carry the signal;
  the contents keep their contrast.
- **A component that is always placed inside a titled section brings no second title.**
- **`ShareLink.vue` is neutral.** It was a success alert, which was wrong where nothing had
  succeeded (a standing property of a list) and wrong where something had (the caller's own
  success alert wrapped it, so green appeared inside green). The caller says whether the occasion
  is a happy one.

---

## Checking a change

The four suites cover behaviour, not appearance. For appearance, drive the running container and
look:

```bash
SUBMISSION_LIMIT_PER_HOUR=1000 docker compose up -d --build
set -a && . ./.env && set +a
# then a short Playwright script that signs in and screenshots each route at 1440px and 375px
```

Both widths, every time. The 375px pass is what catches a row that stops wrapping.
