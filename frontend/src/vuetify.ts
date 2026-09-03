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
  defaults: {
    VTextField: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VTextarea: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VBtn: { variant: 'flat' },
    VCard: { elevation: 1, rounded: 'lg' },
    VAlert: { variant: 'tonal', density: 'comfortable' },
  },
})
