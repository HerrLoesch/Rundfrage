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
      return /^[+-]?\d+$/.test(rawValue)
    case 'decimal':
      return /^[+-]?(\d+\.?\d*|\.\d+)$/.test(rawValue)
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

/** 010 FR-016, FR-017: a required field with no value is invalid, regardless of type. */
export function isAnswered(rawValue: string | null | undefined): boolean {
  return rawValue !== null && rawValue !== undefined && rawValue !== ''
}

export type { FieldType }
