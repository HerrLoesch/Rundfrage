/**
 * Today, in the format an `<input type="date">` wants.
 *
 * A form built around such a field must start it filled rather than empty: WebKit renders an
 * empty one with today's actual digits rather than a distinguishable placeholder, so a field left
 * untouched looks identical to one the operator deliberately set to today. Defaulting to today
 * makes the field's appearance and its value agree from the first render.
 */
export function today(): string {
  const now = new Date()
  return [
    now.getFullYear(),
    String(now.getMonth() + 1).padStart(2, '0'),
    String(now.getDate()).padStart(2, '0'),
  ].join('-')
}

/**
 * Turns a `DateOnly` string like "2026-10-22" into a `Date` fit for display.
 *
 * Handing that string straight to `new Date()` parses it as UTC midnight, which at a
 * UTC-negative offset falls on the *previous* local day - so a date could render one day early.
 * Noon has no timezone offset large enough to cross a day boundary either way.
 */
export function parseDateOnly(value: string): Date {
  return new Date(`${value}T12:00:00`)
}
