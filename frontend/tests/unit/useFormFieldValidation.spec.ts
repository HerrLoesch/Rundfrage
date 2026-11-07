import { describe, expect, it } from 'vitest'
import { isAnswered, isValidValue, normalizeValue } from '../../src/composables/useFormFieldValidation'
import fixture from '../fixtures/form-field-validation.json'

/**
 * The seven type rules of 010 FR-023 to FR-030, checked against the fixture shared with the
 * backend's own implementation (010 research.md R-7) - the mechanism that keeps FR-030's promise
 * ("the two MUST never disagree") true without a shared runtime.
 */
describe('useFormFieldValidation', () => {
  for (const testCase of fixture.cases) {
    const shown = JSON.stringify(testCase.value)
    const name = shown.length > 30 ? `${shown.slice(0, 27)}…"` : shown
    it(`${testCase.type} ${name} → ${testCase.expectedValid}`, () => {
      const field = {
        type: testCase.type as import('../../src/api/client').FieldType,
        maxLength: testCase.field?.maxLength ?? null,
        minLength: testCase.field?.minLength ?? null,
      }

      expect(isValidValue(field, testCase.value)).toBe(testCase.expectedValid)
    })
  }

  for (const testCase of fixture.answeredCases) {
    it(`counts ${JSON.stringify(testCase.value)} as answered: ${testCase.expectedAnswered}`, () => {
      expect(isAnswered(testCase.value)).toBe(testCase.expectedAnswered)
    })
  }

  it('turns a German decimal comma into the point the server accepts', () => {
    const decimal = { type: 'decimal' as const, maxLength: null, minLength: null }

    expect(normalizeValue('decimal', '1,5')).toBe('1.5')
    expect(isValidValue(decimal, normalizeValue('decimal', '1,5'))).toBe(true)
  })

  it('leaves every other type exactly as typed, commas included', () => {
    expect(normalizeValue('text', 'Müller, Anna')).toBe('Müller, Anna')
    expect(normalizeValue('integer', '1,5')).toBe('1,5')
  })

  it('treats an absent value as always valid, because requiredness is a separate check', () => {
    expect(isValidValue({ type: 'email', maxLength: null, minLength: null }, null)).toBe(true)
    expect(isValidValue({ type: 'email', maxLength: null, minLength: null }, undefined)).toBe(true)
  })

  it('covers every field type at least once', () => {
    const covered = new Set(fixture.cases.map((c) => c.type))
    const expected = ['text', 'integer', 'decimal', 'boolean', 'email', 'phone', 'postalCode']

    for (const type of expected) {
      expect(covered.has(type)).toBe(true)
    }
  })
})
