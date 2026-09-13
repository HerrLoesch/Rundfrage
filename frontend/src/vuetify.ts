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
          primary: '#1565c0',
          secondary: '#37474f',
          surface: '#ffffff',
          background: '#f4f6f8',
          error: '#c62828',
          warning: '#ef6c00',
          success: '#2e7d32',
          info: '#0277bd',
          // The three answered states, plus the absence of an answer.
          'state-yes': '#2e7d32',
          'state-maybe': '#ef6c00',
          'state-no': '#c62828',
          'state-none': '#90a4ae',
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
