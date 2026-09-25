import { describe, expect, it } from 'vitest'
import { isValidValue } from '../../src/composables/useFormFieldValidation'
import fixture from '../fixtures/form-field-validation.json'

/**
 * The seven type rules of 010 FR-023 to FR-030, checked against the fixture shared with the
 * backend's own implementation (010 research.md R-7) - the mechanism that keeps FR-030's promise
 * ("the two MUST never disagree") true without a shared runtime.
 */
describe('useFormFieldValidation', () => {
  for (const testCase of fixture.cases) {
    it(`${testCase.type} "${testCase.value}" → ${testCase.expectedValid}`, () => {
      const field = {
        type: testCase.type as import('../../src/api/client').FieldType,
        maxLength: testCase.field?.maxLength ?? null,
        minLength: testCase.field?.minLength ?? null,
      }

      expect(isValidValue(field, testCase.value)).toBe(testCase.expectedValid)
    })
  }

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
