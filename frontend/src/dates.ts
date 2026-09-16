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
