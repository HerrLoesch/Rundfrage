import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import { de } from 'vuetify/locale'

/**
 * One theme, defined once.
 *
 * The three answer states get named colours here rather than hard-coded hex values scattered
 * through components - but colour is never the only carrier of meaning: every state also shows
 * a character (FR-053), so the grid survives greyscale and colour blindness.
 */
export const vuetify = createVuetify({
  components,
  directives,
  locale: {
    locale: 'de',
    messages: { de },
  },
  theme: {
    defaultTheme: 'rundfrage',
    themes: {
      rundfrage: {
        dark: false,
        colors: {
          // Audi-inspired palette: red as the accent, black and white as the foundation.
          primary: '#bb0a30',
          'on-primary': '#ffffff',
          secondary: '#1a1a1a',
          'on-secondary': '#ffffff',
          surface: '#ffffff',
          background: '#f2f2f2',
          error: '#b42318',
          'on-error': '#ffffff',
          warning: '#b54708',
          'on-warning': '#ffffff',
          success: '#166534',
          'on-success': '#ffffff',
          info: '#075985',
          'on-info': '#ffffff',
          // The three answered states, plus the absence of an answer.
          'state-yes': '#166534',
          'on-state-yes': '#ffffff',
          'state-maybe': '#a15c00',
          'on-state-maybe': '#ffffff',
          'state-no': '#b42318',
          'on-state-no': '#ffffff',
          'state-none': '#667085',
          'on-state-none': '#ffffff',
        },
      },
    },
  },
  /**
   * The component defaults are half of the visual consistency; `styles/app.css` is the other.
   *
   * Set here rather than repeated per call site, because a default that lives in twelve templates
   * is twelve chances to disagree - which is what a card with `elevation-1` beside one with
   * `variant="outlined"` beside one with neither had already produced.
   */
  defaults: {
    VTextField: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VTextarea: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VSelect: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VFileInput: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VBtn: { variant: 'flat' },

    // A hairline border rather than a shadow. Shadows stack visually: a card inside a card
    // inside an alert produced three overlapping glows and no hierarchy at all. A border draws
    // exactly one line whatever it is nested in.
    VCard: { elevation: 0, rounded: 'lg', border: 'thin' },
    VAlert: { variant: 'tonal', density: 'comfortable' },
    VDialog: { maxWidth: 520 },
    VChip: { size: 'small' },
  },
})
