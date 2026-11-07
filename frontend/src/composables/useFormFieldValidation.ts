import type { FieldType, FormFieldDetail } from '../api/client'

/**
 * Whether one entered value matches its field's type (010 FR-016, FR-023 to FR-030).
 *
 * This is one half of a rule table written twice - the other half is
 * `backend/src/Rundfrage.Api/Forms/FormFieldValidation.cs` - kept from silently disagreeing by a
 * shared test fixture (`frontend/tests/fixtures/form-field-validation.json`) rather than a shared
 * runtime (010 research.md R-7, FR-030).
 */
export function isValidValue(
  field: Pick<FormFieldDetail, 'type' | 'maxLength' | 'minLength'>,
  rawValue: string | null | undefined,
): boolean {
  if (rawValue === null || rawValue === undefined) {
    // Unanswered is valid for every type; requiredness is a separate check.
    return true
  }

  switch (field.type) {
    case 'text':
      return isValidText(field, rawValue)
    case 'integer':
      return isValidInteger(rawValue)
    case 'decimal':
      return isValidDecimal(rawValue)
    case 'boolean':
      // 010 FR-026: exactly "yes" or "no" - matching the requirement's wording and this
      // codebase's existing wire vocabulary for answer-shaped booleans.
      return rawValue === 'yes' || rawValue === 'no'
    case 'email':
      return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(rawValue)
    case 'phone':
      return isValidPhone(rawValue)
    case 'postalCode':
      return /^\d{5}$/.test(rawValue)
    default:
      return assertNever(field.type)
  }
}

const INT64_MIN = -(2n ** 63n)
const INT64_MAX = 2n ** 63n - 1n

/** Digits with an optional sign, within the 64-bit range the server and the export use. */
function isValidInteger(value: string): boolean {
  if (!/^[+-]?\d+$/.test(value)) return false
  const parsed = BigInt(value.replace(/^\+/, ''))
  return parsed >= INT64_MIN && parsed <= INT64_MAX
}

/** Positional notation that stays finite once parsed - a few hundred digits overflow to Infinity. */
function isValidDecimal(value: string): boolean {
  return /^[+-]?(\d+\.?\d*|\.\d+)$/.test(value) && Number.isFinite(Number(value))
}

function isValidText(
  field: Pick<FormFieldDetail, 'maxLength' | 'minLength'>,
  value: string,
): boolean {
  if (field.minLength != null && value.length < field.minLength) return false
  if (field.maxLength != null && value.length > field.maxLength) return false
  return true
}

/** 010 FR-028: digits, an optional leading "+", and interior spaces, dashes or parentheses. */
function isValidPhone(value: string): boolean {
  let digitCount = 0

  for (let i = 0; i < value.length; i++) {
    const c = value[i]

    if (c >= '0' && c <= '9') {
      digitCount++
      continue
    }
    if (c === '+' && i === 0) continue
    if (c === ' ' || c === '-' || c === '(' || c === ')') continue

    return false
  }

  return digitCount >= 7
}

function assertNever(value: never): never {
  throw new Error(`Unhandled field type: ${String(value)}`)
}

/**
 * 010 FR-016, FR-017: a required field with no value is invalid, regardless of type. Empty or
 * whitespace-only text is not an answer, matching the server's `FormFieldValidation.IsAnswered`.
 */
export function isAnswered(rawValue: string | null | undefined): boolean {
  return rawValue !== null && rawValue !== undefined && rawValue.trim() !== ''
}

/**
 * The value as it is validated and sent. A decimal typed the German way ("1,5") becomes "1.5":
 * the wire format stays the culture-invariant one the server checks (data-model.md §5), but a
 * participant is not refused for using the separator their keyboard offers.
 */
export function normalizeValue(type: FieldType, rawValue: string): string {
  return type === 'decimal' ? rawValue.replace(',', '.') : rawValue
}

export type { FieldType }
