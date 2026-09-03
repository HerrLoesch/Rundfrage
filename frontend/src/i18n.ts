import { createI18n } from 'vue-i18n'
import de from './locales/de.json'

/**
 * All user-facing text is resolved through this layer; components carry no literal
 * strings (FR-029). Only German exists today (FR-028).
 *
 * Note: this indirection is a recorded deviation from constitution Principle III, which
 * requires abstractions to follow a second concrete use. See the Constitution Deviations
 * section of spec.md and the Complexity Tracking table in plan.md.
 */
export const i18n = createI18n({
  legacy: false,
  locale: 'de',
  fallbackLocale: 'de',
  messages: { de },
  datetimeFormats: {
    de: {
      short: { day: '2-digit', month: '2-digit' },
      long: { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' },
    },
  },
})
